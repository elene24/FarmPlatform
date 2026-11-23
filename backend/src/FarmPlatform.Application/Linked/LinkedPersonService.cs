using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Links;
using FarmPlatform.Application.Common.Persistence;
using FarmPlatform.Application.Common.Results;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Application.Common.Sms;
using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Linked;
using FarmPlatform.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace FarmPlatform.Application.Linked;

public sealed class LinkedPersonService(
    IAppDbContext db,
    ISensitiveDataProtector protector,
    ISmsSender sms,
    IAppLinks links,
    IUserDirectory users,
    TimeProvider timeProvider) : ILinkedPersonService
{
    public async Task<IReadOnlyList<LinkedPersonDto>> ListAsync(Guid representativeId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var people = await db.LinkedPersons.AsNoTracking()
            .Where(l => l.RepresentativeUserId == representativeId)
            .OrderBy(l => l.LastName).ThenBy(l => l.FirstName)
            .ToListAsync(cancellationToken);
        return people.Select(p => ToDto(p, now)).ToList();
    }

    public async Task<LinkedPersonDto?> GetAsync(Guid representativeId, Guid id, CancellationToken cancellationToken)
    {
        var person = await FindAsync(representativeId, id, cancellationToken);
        return person is null ? null : ToDto(person, timeProvider.GetUtcNow());
    }

    public async Task<Result<LinkedPersonDto>> CreateAsync(Guid representativeId, LinkedPersonInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = LinkedPersonValidator.Validate(input, isNew: true, hasStoredBankAccount: false);
        if (errors.Count > 0)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Validation, [.. errors]);
        }

        var representative = await db.PersonalDetails.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == representativeId, cancellationToken);
        if (representative is null)
        {
            // Representatives must identify themselves first (name, ID, bank account).
            return Result.Failure<LinkedPersonDto>(ErrorKind.Conflict, new AppError("linked.representative_details_required"));
        }

        var count = await db.LinkedPersons.CountAsync(l => l.RepresentativeUserId == representativeId, cancellationToken);
        if (count >= LinkedPerson.MaxPerRepresentative)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Conflict, new AppError("linked.limit_reached"));
        }

        var checks = await CheckIdentityAsync(representativeId, representative, null, input, cancellationToken);
        if (checks is not null)
        {
            return Result.Failure<LinkedPersonDto>(checks.Value.Kind, checks.Value.Error);
        }

        var pin = protector.Protect(input.PersonalIdNumber!, SensitiveDataKind.PersonalIdNumber, withBlindIndex: true);
        var token = ConsentSecrets.NewToken();
        LinkedPerson person;
        try
        {
            person = new LinkedPerson(representativeId, input.FirstName!, input.LastName!, pin, input.LegalAddress!, input.ActualAddress!,
                new GeoPoint(input.Latitude!.Value, input.Longitude!.Value), ConsentSecrets.HashToken(token));
            person.SetContactAndPayout(NormalizedPhone(input), input.PayToRepresentative, ProtectIban(input));
        }
        catch (DomainException ex)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Validation, new AppError(ex.Code));
        }

        db.LinkedPersons.Add(person);
        var code = IssueCode(person);
        if (!await SaveAsync(cancellationToken))
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Conflict, new AppError("profile.personal_id_taken", "personalIdNumber"));
        }

        await SendConsentSmsAsync(person, representative, code, token, cancellationToken);
        return Result.Success(ToDto(person, timeProvider.GetUtcNow()));
    }

    public async Task<Result<LinkedPersonDto>> UpdateAsync(Guid representativeId, Guid id, LinkedPersonInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var person = await FindAsync(representativeId, id, cancellationToken, tracking: true);
        if (person is null)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.NotFound, new AppError("linked.not_found"));
        }

        var errors = LinkedPersonValidator.Validate(input, isNew: false, hasStoredBankAccount: person.BankAccountCiphertext is not null);
        if (errors.Count > 0)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Validation, [.. errors]);
        }

        var representative = await db.PersonalDetails.AsNoTracking().FirstAsync(p => p.UserId == representativeId, cancellationToken);
        var checks = await CheckIdentityAsync(representativeId, representative, person.Id, input, cancellationToken);
        if (checks is not null)
        {
            return Result.Failure<LinkedPersonDto>(checks.Value.Kind, checks.Value.Error);
        }

        bool consentChanged;
        try
        {
            person.UpdateDetails(input.FirstName!, input.LastName!, input.LegalAddress!, input.ActualAddress!, new GeoPoint(input.Latitude!.Value, input.Longitude!.Value));
            if (!string.IsNullOrWhiteSpace(input.PersonalIdNumber))
            {
                person.SetPersonalIdNumber(protector.Protect(input.PersonalIdNumber, SensitiveDataKind.PersonalIdNumber, withBlindIndex: true));
            }

            consentChanged = person.SetContactAndPayout(NormalizedPhone(input), input.PayToRepresentative, ProtectIban(input));
        }
        catch (DomainException ex)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Validation, new AppError(ex.Code));
        }

        // What the person agreed to changed: ask again (if an SMS may be sent right now).
        string? code = null;
        if (consentChanged && person.CanSendConsentSms(timeProvider.GetUtcNow(), requestChanged: true))
        {
            code = IssueCode(person, requestChanged: true);
        }

        if (!await SaveAsync(cancellationToken))
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Conflict, new AppError("profile.personal_id_taken", "personalIdNumber"));
        }

        if (code is not null)
        {
            await SendConsentSmsAsync(person, representative, code, token: null, cancellationToken);
        }

        return Result.Success(ToDto(person, timeProvider.GetUtcNow()));
    }

    public async Task<Result> DeleteAsync(Guid representativeId, Guid id, CancellationToken cancellationToken)
    {
        var person = await FindAsync(representativeId, id, cancellationToken, tracking: true);
        if (person is null)
        {
            return Result.Failure(ErrorKind.NotFound, new AppError("linked.not_found"));
        }

        var representative = await db.PersonalDetails.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == representativeId, cancellationToken);
        var phone = person.PhoneNumber;
        var wasLinked = person.ConsentStatus is ConsentStatus.Confirmed or ConsentStatus.Pending;
        db.LinkedPersons.Remove(person);
        await db.SaveChangesAsync(cancellationToken);

        if (wasLinked && representative is not null)
        {
            await SendSafelyAsync(phone, ConsentTexts.RepresentationEndedSms(FullName(representative)), cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<LinkedPersonDto>> ResendConsentAsync(Guid representativeId, Guid id, CancellationToken cancellationToken)
    {
        var person = await FindAsync(representativeId, id, cancellationToken, tracking: true);
        if (person is null)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.NotFound, new AppError("linked.not_found"));
        }

        string code;
        try
        {
            code = IssueCode(person);
        }
        catch (DomainException ex)
        {
            return Result.Failure<LinkedPersonDto>(ex.Code == "linked.sms_too_soon" ? ErrorKind.Conflict : ErrorKind.Validation, new AppError(ex.Code));
        }

        await db.SaveChangesAsync(cancellationToken);
        var representative = await db.PersonalDetails.AsNoTracking().FirstAsync(p => p.UserId == representativeId, cancellationToken);
        await SendConsentSmsAsync(person, representative, code, token: null, cancellationToken);
        return Result.Success(ToDto(person, timeProvider.GetUtcNow()));
    }

    public async Task<Result<LinkedPersonDto>> ConfirmWithCodeAsync(Guid representativeId, Guid id, string? code, CancellationToken cancellationToken)
    {
        var person = await FindAsync(representativeId, id, cancellationToken, tracking: true);
        if (person is null)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.NotFound, new AppError("linked.not_found"));
        }

        var digits = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != 6)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Validation, new AppError("linked.code_invalid", "code"));
        }

        bool ok;
        try
        {
            ok = person.TryConfirmWithCode(ConsentSecrets.HashCode(person.Id, digits), ConsentTexts.Version, timeProvider.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return Result.Failure<LinkedPersonDto>(ErrorKind.Validation, new AppError(ex.Code, "code"));
        }

        await db.SaveChangesAsync(cancellationToken); // also stores failed attempts
        return ok
            ? Result.Success(ToDto(person, timeProvider.GetUtcNow()))
            : Result.Failure<LinkedPersonDto>(ErrorKind.Validation, new AppError("linked.code_invalid", "code"));
    }

    public async Task<ConsentRequestDto?> GetConsentRequestAsync(string token, CancellationToken cancellationToken)
    {
        var person = await FindByTokenAsync(token, tracking: false, cancellationToken);
        return person is null ? null : await ToConsentDtoAsync(person, cancellationToken);
    }

    public async Task<Result<ConsentRequestDto>> RespondAsync(string token, ConsentResponse response, CancellationToken cancellationToken)
    {
        var person = await FindByTokenAsync(token, tracking: true, cancellationToken);
        if (person is null)
        {
            return Result.Failure<ConsentRequestDto>(ErrorKind.NotFound, new AppError("linked.consent_not_found"));
        }

        var now = timeProvider.GetUtcNow();
        switch (response)
        {
            case ConsentResponse.Accept:
                person.ConfirmViaLink(ConsentTexts.Version, now);
                break;
            case ConsentResponse.Decline:
                person.Decline(now);
                break;
            case ConsentResponse.Withdraw:
                person.Withdraw(now);
                break;
            default:
                return Result.Failure<ConsentRequestDto>(ErrorKind.Validation, new AppError("validation.invalid"));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToConsentDtoAsync(person, cancellationToken));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Identity rules: one person, one profile (the ID may not belong to an account or another linked person),
    /// a representative cannot link themselves, and the consent SMS cannot go to the representative's own phone.
    /// </summary>
    private async Task<(ErrorKind Kind, AppError Error)?> CheckIdentityAsync(
        Guid representativeId, PersonalDetails representative, Guid? selfId, LinkedPersonInput input, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(input.PersonalIdNumber))
        {
            var hash = protector.ComputeBlindIndex(input.PersonalIdNumber, SensitiveDataKind.PersonalIdNumber);
            if (hash == representative.PersonalIdNumberHash)
            {
                return (ErrorKind.Validation, new AppError("linked.cannot_link_self", "personalIdNumber"));
            }

            var taken = await db.PersonalDetails.AnyAsync(p => p.PersonalIdNumberHash == hash, cancellationToken)
                || await db.LinkedPersons.AnyAsync(l => l.PersonalIdNumberHash == hash && l.Id != selfId, cancellationToken);
            if (taken)
            {
                return (ErrorKind.Conflict, new AppError("profile.personal_id_taken", "personalIdNumber"));
            }
        }

        var phone = NormalizedPhone(input);
        var ownPhone = await users.GetPhoneNumberAsync(representativeId, cancellationToken);
        if (phone == ownPhone || phone == representative.AlternativePhoneNumber)
        {
            return (ErrorKind.Validation, new AppError("linked.phone_is_representatives", "phoneNumber"));
        }

        return null;
    }

    private string IssueCode(LinkedPerson person, bool requestChanged = false)
    {
        var code = ConsentSecrets.NewCode();
        person.IssueConsentCode(ConsentSecrets.HashCode(person.Id, code), timeProvider.GetUtcNow(), requestChanged);
        return code;
    }

    /// <summary>
    /// Sends the consent request. Only hashes of tokens are stored, so every later SMS gets a fresh link token;
    /// the newest SMS always has a working link (to agree, decline or withdraw).
    /// </summary>
    private async Task SendConsentSmsAsync(LinkedPerson person, PersonalDetails representative, string code, string? token, CancellationToken cancellationToken)
    {
        if (token is null)
        {
            token = ConsentSecrets.NewToken();
            person.RotateConsentToken(ConsentSecrets.HashToken(token));
            await db.SaveChangesAsync(cancellationToken);
        }

        var text = ConsentTexts.ConsentRequestSms(FullName(representative), person.FirstName, person.PayToRepresentative, person.BankAccountLast4, code, links.Consent(token));
        await SendSafelyAsync(person.PhoneNumber, text, cancellationToken);
    }

    private async Task SendSafelyAsync(string phone, string text, CancellationToken cancellationToken)
    {
        try
        {
            await sms.SendAsync(phone, text, cancellationToken);
        }
#pragma warning disable CA1031 // A failed SMS must not undo the saved change; the representative can resend.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private async Task<LinkedPerson?> FindAsync(Guid representativeId, Guid id, CancellationToken cancellationToken, bool tracking = false)
    {
        var query = tracking ? db.LinkedPersons : db.LinkedPersons.AsNoTracking();
        return await query.FirstOrDefaultAsync(l => l.Id == id && l.RepresentativeUserId == representativeId, cancellationToken);
    }

    private async Task<LinkedPerson?> FindByTokenAsync(string token, bool tracking, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
        {
            return null;
        }

        var hash = ConsentSecrets.HashToken(token);
        var query = tracking ? db.LinkedPersons : db.LinkedPersons.AsNoTracking();
        return await query.FirstOrDefaultAsync(l => l.ConsentTokenHash == hash, cancellationToken);
    }

    private async Task<ConsentRequestDto> ToConsentDtoAsync(LinkedPerson person, CancellationToken cancellationToken)
    {
        var representative = await db.PersonalDetails.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == person.RepresentativeUserId, cancellationToken);
        return new ConsentRequestDto(
            person.FirstName,
            representative is null ? "—" : FullName(representative),
            person.PayToRepresentative,
            person.PayToRepresentative ? representative?.BankAccountLast4 : person.BankAccountLast4,
            StatusName(person.ConsentStatus));
    }

    private async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false; // unique personal ID index: two requests for the same person at once
        }
    }

    private ProtectedValue? ProtectIban(LinkedPersonInput input) =>
        input.PayToRepresentative || string.IsNullOrWhiteSpace(input.BankAccountNumber)
            ? null
            : protector.Protect(input.BankAccountNumber, SensitiveDataKind.BankAccountNumber, withBlindIndex: false);

    private static string NormalizedPhone(LinkedPersonInput input) =>
        PhoneNumberNormalizer.TryNormalize(input.PhoneNumber, out var phone) ? phone : string.Empty;

    private static string FullName(PersonalDetails p) => $"{p.FirstName} {p.LastName}";

    private static string StatusName(ConsentStatus status) => status.ToString().ToLowerInvariant();

    private static LinkedPersonDto ToDto(LinkedPerson p, DateTimeOffset now) => new(
        p.Id, p.FirstName, p.LastName, p.PersonalIdNumberLast4, p.PhoneNumber, p.LegalAddress, p.ActualAddress,
        p.Location.Latitude, p.Location.Longitude, p.PayToRepresentative, p.BankAccountLast4,
        StatusName(p.ConsentStatus), p.ConsentMethod?.ToString().ToLowerInvariant(), p.ConsentedAt, p.ConsentWithdrawnAt,
        p.LastConsentSmsAt, p.ConsentStatus != ConsentStatus.Confirmed && p.CanSendConsentSms(now), p.CreatedAt);
}
