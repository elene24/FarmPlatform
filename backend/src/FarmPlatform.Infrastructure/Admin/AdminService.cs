using FarmPlatform.Application.Admin;
using FarmPlatform.Application.Common.Results;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Application.Linked;
using FarmPlatform.Application.Profiles;
using FarmPlatform.Domain.Linked;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Infrastructure.Identity;
using FarmPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FarmPlatform.Infrastructure.Admin;

/// <summary>
/// Admin queries live in Infrastructure because they read Identity's user and role tables directly.
/// Viewing an account's details is logged (who looked at whom), since it shows personal data.
/// </summary>
public sealed partial class AdminService(
    AppDbContext db,
    IProfileService profiles,
    ILinkedPersonService linked,
    ISensitiveDataProtector protector,
    ILogger<AdminService> logger) : IAdminService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private static readonly string[] UserRoleFilters = ["all", "picker", "driver", "orchard_owner", "admin", "none"];
    private static readonly string[] LinkedStatusFilters = ["all", "pending", "confirmed", "declined", "revoked"];

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var adminRoleId = await AdminRoleIdAsync(cancellationToken);
        return new AdminStatsDto(
            await db.Users.CountAsync(cancellationToken),
            await db.PickerProfiles.CountAsync(p => p.IsActive, cancellationToken),
            await db.DriverProfiles.CountAsync(p => p.IsActive, cancellationToken),
            await db.OrchardOwnerProfiles.CountAsync(p => p.IsActive, cancellationToken),
            await db.UserRoles.CountAsync(r => r.RoleId == adminRoleId, cancellationToken),
            await db.LinkedPersons.CountAsync(cancellationToken),
            await db.LinkedPersons.CountAsync(l => l.ConsentStatus == ConsentStatus.Confirmed, cancellationToken));
    }

    public async Task<Result<PagedResult<AdminUserDto>>> ListUsersAsync(AdminUserQuery query, CancellationToken cancellationToken)
    {
        var role = (query.Role ?? "all").Trim().ToLowerInvariant();
        if (!UserRoleFilters.Contains(role))
        {
            return Result.Failure<PagedResult<AdminUserDto>>(ErrorKind.Validation, new AppError("validation.invalid", "role"));
        }

        var (page, pageSize) = Paging(query.Page, query.PageSize);
        var adminRoleId = await AdminRoleIdAsync(cancellationToken);
        var users = db.Users.AsNoTracking();

        users = role switch
        {
            "picker" => users.Where(u => db.PickerProfiles.Any(p => p.UserId == u.Id && p.IsActive)),
            "driver" => users.Where(u => db.DriverProfiles.Any(p => p.UserId == u.Id && p.IsActive)),
            "orchard_owner" => users.Where(u => db.OrchardOwnerProfiles.Any(p => p.UserId == u.Id && p.IsActive)),
            "admin" => users.Where(u => db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId)),
            "none" => users.Where(u =>
                !db.PickerProfiles.Any(p => p.UserId == u.Id && p.IsActive)
                && !db.DriverProfiles.Any(p => p.UserId == u.Id && p.IsActive)
                && !db.OrchardOwnerProfiles.Any(p => p.UserId == u.Id && p.IsActive)),
            _ => users,
        };

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            users = ApplyUserSearch(users, search);
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email!,
                u.PhoneNumber,
                u.EmailConfirmed,
                u.IsActive,
                db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId),
                db.PersonalDetails.Where(p => p.UserId == u.Id).Select(p => p.FirstName).FirstOrDefault(),
                db.PersonalDetails.Where(p => p.UserId == u.Id).Select(p => p.LastName).FirstOrDefault(),
                db.PickerProfiles.Any(p => p.UserId == u.Id && p.IsActive),
                db.DriverProfiles.Any(p => p.UserId == u.Id && p.IsActive),
                db.OrchardOwnerProfiles.Any(p => p.UserId == u.Id && p.IsActive),
                db.LinkedPersons.Count(l => l.RepresentativeUserId == u.Id),
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<AdminUserDto>(items, total, page, pageSize));
    }

    public async Task<AdminUserDetailDto?> GetUserAsync(Guid adminId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        LogViewedUser(logger, adminId, userId);
        var summary = await profiles.GetSummaryAsync(userId, cancellationToken);
        var linkedPickers = await linked.ListAsync(userId, cancellationToken);
        var adminRoleId = await AdminRoleIdAsync(cancellationToken);
        var account = new AdminUserDto(
            user.Id,
            user.Email!,
            user.PhoneNumber,
            user.EmailConfirmed,
            user.IsActive,
            await db.UserRoles.AnyAsync(r => r.UserId == userId && r.RoleId == adminRoleId, cancellationToken),
            summary.PersonalDetails?.FirstName,
            summary.PersonalDetails?.LastName,
            summary.Picker.Status.Active,
            summary.Driver.Status.Active,
            summary.OrchardOwner.Status.Active,
            linkedPickers.Count,
            user.CreatedAt);

        return new AdminUserDetailDto(account, user.PreferredLanguage, user.TermsAcceptedAt, summary, linkedPickers);
    }

    public async Task<Result<PagedResult<AdminLinkedPickerDto>>> ListLinkedPickersAsync(AdminLinkedQuery query, CancellationToken cancellationToken)
    {
        var status = (query.Status ?? "all").Trim().ToLowerInvariant();
        if (!LinkedStatusFilters.Contains(status))
        {
            return Result.Failure<PagedResult<AdminLinkedPickerDto>>(ErrorKind.Validation, new AppError("validation.invalid", "status"));
        }

        var (page, pageSize) = Paging(query.Page, query.PageSize);
        var people = db.LinkedPersons.AsNoTracking();
        if (status != "all")
        {
            var wanted = Enum.Parse<ConsentStatus>(status, ignoreCase: true);
            people = people.Where(l => l.ConsentStatus == wanted);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var digits = new string(search.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length == 11 && digits.Length == search.Length)
            {
                var hash = protector.ComputeBlindIndex(digits, SensitiveDataKind.PersonalIdNumber);
                people = people.Where(l => l.PersonalIdNumberHash == hash);
            }
            else
            {
                var pattern = Like(search);
                var phone = digits.Length >= 4 ? Like(digits) : null;
                people = people.Where(l =>
                    EF.Functions.ILike(l.FirstName, pattern, "\\")
                    || EF.Functions.ILike(l.LastName, pattern, "\\")
                    || (phone != null && EF.Functions.ILike(l.PhoneNumber, phone, "\\"))
                    || db.Users.Any(u => u.Id == l.RepresentativeUserId && EF.Functions.ILike(u.Email!, pattern, "\\")));
            }
        }

        var total = await people.CountAsync(cancellationToken);
        var rows = await people
            .OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.FirstName,
                l.LastName,
                l.PersonalIdNumberLast4,
                l.PhoneNumber,
                l.ActualAddress,
                l.PayToRepresentative,
                l.ConsentStatus,
                l.ConsentedAt,
                l.CreatedAt,
                l.RepresentativeUserId,
                RepresentativeEmail = db.Users.Where(u => u.Id == l.RepresentativeUserId).Select(u => u.Email).FirstOrDefault(),
                RepresentativeFirstName = db.PersonalDetails.Where(p => p.UserId == l.RepresentativeUserId).Select(p => p.FirstName).FirstOrDefault(),
                RepresentativeLastName = db.PersonalDetails.Where(p => p.UserId == l.RepresentativeUserId).Select(p => p.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminLinkedPickerDto(
            r.Id, r.FirstName, r.LastName, r.PersonalIdNumberLast4, r.PhoneNumber, r.ActualAddress, r.PayToRepresentative,
            r.ConsentStatus.ToString().ToLowerInvariant(), r.ConsentedAt, r.CreatedAt,
            r.RepresentativeUserId, r.RepresentativeEmail ?? string.Empty, r.RepresentativeFirstName, r.RepresentativeLastName)).ToList();

        return Result.Success(new PagedResult<AdminLinkedPickerDto>(items, total, page, pageSize));
    }

    /// <summary>Search by name, email or phone; an exact 11-digit personal ID number is matched through its blind index.</summary>
    private IQueryable<ApplicationUser> ApplyUserSearch(IQueryable<ApplicationUser> users, string search)
    {
        var digits = new string(search.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 11 && digits.Length == search.Length)
        {
            var hash = protector.ComputeBlindIndex(digits, SensitiveDataKind.PersonalIdNumber);
            return users.Where(u => db.PersonalDetails.Any(p => p.UserId == u.Id && p.PersonalIdNumberHash == hash));
        }

        var pattern = Like(search);
        var phone = digits.Length >= 4 ? Like(digits) : null;
        return users.Where(u =>
            EF.Functions.ILike(u.Email!, pattern, "\\")
            || (phone != null && u.PhoneNumber != null && EF.Functions.ILike(u.PhoneNumber, phone, "\\"))
            || db.PersonalDetails.Any(p => p.UserId == u.Id
                && (EF.Functions.ILike(p.FirstName, pattern, "\\") || EF.Functions.ILike(p.LastName, pattern, "\\"))));
    }

    private static readonly string NormalizedAdminRole = AppRoles.Admin.ToUpperInvariant();

    private async Task<Guid?> AdminRoleIdAsync(CancellationToken cancellationToken) =>
        await db.Roles.Where(r => r.NormalizedName == NormalizedAdminRole).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);

    private static (int Page, int PageSize) Paging(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

    private static string Like(string value) =>
        "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {AdminId} viewed account {UserId}")]
    private static partial void LogViewedUser(ILogger logger, Guid adminId, Guid userId);
}
