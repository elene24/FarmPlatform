using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Persistence;
using FarmPlatform.Application.Common.Results;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Orchards;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace FarmPlatform.Application.Profiles;

public sealed class ProfileService(IAppDbContext db, ISensitiveDataProtector protector, TimeProvider timeProvider) : IProfileService
{
    // ------------------------------------------------------------------ summary

    public async Task<ProfilesSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var personal = await db.PersonalDetails.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var picker = await db.PickerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var driver = await db.DriverProfiles.AsNoTracking().Include(d => d.Vehicles).FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        var owner = await db.OrchardOwnerProfiles.AsNoTracking().Include(o => o.Orchards).FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);
        var statusCodes = await StatusCodesAsync(cancellationToken);

        return new ProfilesSummaryDto(
            personal is null ? null : ToDto(personal),
            new PickerSummaryDto(Status(picker)),
            new DriverSummaryDto(Status(driver), driver?.Vehicles.OrderBy(v => v.CreatedAt).ThenBy(v => v.LicensePlateNumber, StringComparer.Ordinal).Select(ToDto).ToList() ?? []),
            new OrchardOwnerSummaryDto(Status(owner), owner?.Orchards.OrderBy(o => o.CreatedAt).ThenBy(o => o.Name, StringComparer.Ordinal).Select(o => ToDto(o, statusCodes)).ToList() ?? []));
    }

    // ------------------------------------------------------------------ registration (wizard steps 3-5)

    public async Task<Result<ProfilesSummaryDto>> RegisterCategoriesAsync(Guid userId, CategoryRegistrationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = new List<AppError>();

        if (!command.Picker && !command.Driver && !command.OrchardOwner)
        {
            return Result.Failure<ProfilesSummaryDto>(ErrorKind.Validation, new AppError("profile.no_category_selected", "categories"));
        }

        var personal = await db.PersonalDetails.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var picker = await db.PickerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var driver = await db.DriverProfiles.Include(d => d.Vehicles).FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        var owner = await db.OrchardOwnerProfiles.Include(o => o.Orchards).FirstOrDefaultAsync(o => o.UserId == userId, cancellationToken);

        // Personal details: required for Picker/Driver when the user has none; optional update otherwise.
        var needsPersonal = command.Picker || command.Driver;
        if (needsPersonal && (personal is null || command.PersonalDetails is not null))
        {
            ProfileValidator.ValidatePersonalDetails(command.PersonalDetails, isNew: personal is null, personal?.AccountHolderPersonalIdCiphertext is not null, "personalDetails.", errors);
        }

        // Vehicles: a new driver needs at least one.
        var vehicles = command.Driver ? command.Vehicles ?? [] : [];
        if (command.Driver && vehicles.Count == 0 && (driver is null || driver.Vehicles.Count == 0))
        {
            errors.Add(new AppError("profile.vehicle_required", "vehicles"));
        }

        for (var i = 0; i < vehicles.Count; i++)
        {
            ProfileValidator.ValidateVehicle(vehicles[i], $"vehicles.{i}.", errors);
        }

        // Orchards: a new orchard owner needs at least one.
        var orchards = command.OrchardOwner ? command.Orchards ?? [] : [];
        if (command.OrchardOwner && orchards.Count == 0 && (owner is null || owner.Orchards.Count == 0))
        {
            errors.Add(new AppError("profile.orchard_required", "orchards"));
        }

        var statuses = await ActiveStatusCodesAsync(cancellationToken);
        for (var i = 0; i < orchards.Count; i++)
        {
            ProfileValidator.ValidateOrchard(orchards[i], StatusCode(orchards[i]?.LegalEntityStatusId, statuses), $"orchards.{i}.", errors);
        }

        if (errors.Count > 0)
        {
            return Result.Failure<ProfilesSummaryDto>(ErrorKind.Validation, [.. errors]);
        }

        if (needsPersonal && command.PersonalDetails is not null)
        {
            var saved = await ApplyPersonalDetailsAsync(userId, personal, command.PersonalDetails, "personalDetails.", cancellationToken);
            if (!saved.IsSuccess)
            {
                return Result.Failure<ProfilesSummaryDto>(saved.Kind!.Value, [.. saved.Errors]);
            }
        }

        try
        {
            if (command.Picker)
            {
                Activate(picker, () => db.PickerProfiles.Add(new PickerProfile(userId)).Entity);
            }

            if (command.Driver)
            {
                driver = Activate(driver, () => db.DriverProfiles.Add(new DriverProfile(userId)).Entity);
                foreach (var v in vehicles)
                {
                    var added = driver.AddVehicle(v.LicensePlateNumber!, v.Model!, v.PassengerCapacity!.Value);
                    db.Vehicles.Add(added);
                }
            }

            if (command.OrchardOwner)
            {
                owner = Activate(owner, () => db.OrchardOwnerProfiles.Add(new OrchardOwnerProfile(userId)).Entity);
                foreach (var o in orchards)
                {
                    db.Orchards.Add(owner.AddOrchard(ToDetails(o)));
                }
            }
        }
        catch (DomainException ex)
        {
            return Result.Failure<ProfilesSummaryDto>(ErrorKind.Validation, new AppError(ex.Code));
        }

        // One SaveChanges = one transaction: either the whole registration is stored or nothing is.
        var conflict = await SaveAsync(cancellationToken);
        if (conflict is not null)
        {
            return Result.Failure<ProfilesSummaryDto>(ErrorKind.Conflict, conflict);
        }

        return Result.Success(await GetSummaryAsync(userId, cancellationToken));
    }

    public async Task<Result> DeactivateCategoryAsync(Guid userId, ProfileCategory category, CancellationToken cancellationToken)
    {
        RoleProfile? profile = category switch
        {
            ProfileCategory.Picker => await db.PickerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken),
            ProfileCategory.Driver => await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken),
            ProfileCategory.OrchardOwner => await db.OrchardOwnerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken),
            _ => null,
        };

        if (profile is null || !profile.IsActive)
        {
            return Result.Failure(ErrorKind.NotFound, new AppError("profile.not_registered"));
        }

        profile.Deactivate(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ------------------------------------------------------------------ personal details

    public async Task<PersonalDetailsDto?> GetPersonalDetailsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var personal = await db.PersonalDetails.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return personal is null ? null : ToDto(personal);
    }

    public async Task<Result<PersonalDetailsDto>> SavePersonalDetailsAsync(Guid userId, PersonalDetailsInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var personal = await db.PersonalDetails.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var errors = new List<AppError>();
        ProfileValidator.ValidatePersonalDetails(input, isNew: personal is null, personal?.AccountHolderPersonalIdCiphertext is not null, string.Empty, errors);
        if (errors.Count > 0)
        {
            return Result.Failure<PersonalDetailsDto>(ErrorKind.Validation, [.. errors]);
        }

        var applied = await ApplyPersonalDetailsAsync(userId, personal, input, string.Empty, cancellationToken);
        if (!applied.IsSuccess)
        {
            return Result.Failure<PersonalDetailsDto>(applied.Kind!.Value, [.. applied.Errors]);
        }

        var conflict = await SaveAsync(cancellationToken);
        return conflict is null
            ? Result.Success(ToDto(applied.Value!))
            : Result.Failure<PersonalDetailsDto>(ErrorKind.Conflict, conflict);
    }

    // ------------------------------------------------------------------ vehicles

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Vehicles.AsNoTracking()
            .Where(v => db.DriverProfiles.Any(d => d.Id == v.DriverProfileId && d.UserId == userId))
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.LicensePlateNumber)
            .Select(v => new VehicleDto(v.Id, v.LicensePlateNumber, v.Model, v.PassengerCapacity))
            .ToListAsync(cancellationToken);

    public async Task<Result<VehicleDto>> AddVehicleAsync(Guid userId, VehicleInput input, CancellationToken cancellationToken)
    {
        var errors = new List<AppError>();
        ProfileValidator.ValidateVehicle(input, string.Empty, errors);
        if (errors.Count > 0)
        {
            return Result.Failure<VehicleDto>(ErrorKind.Validation, [.. errors]);
        }

        var driver = await db.DriverProfiles.Include(d => d.Vehicles).FirstOrDefaultAsync(d => d.UserId == userId && d.IsActive, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<VehicleDto>(ErrorKind.NotFound, new AppError("profile.not_registered"));
        }

        return await MutateAsync(() =>
        {
            var vehicle = driver.AddVehicle(input.LicensePlateNumber!, input.Model!, input.PassengerCapacity!.Value);
            db.Vehicles.Add(vehicle);
            return ToDto(vehicle);
        }, cancellationToken);
    }

    public async Task<Result<VehicleDto>> UpdateVehicleAsync(Guid userId, Guid vehicleId, VehicleInput input, CancellationToken cancellationToken)
    {
        var errors = new List<AppError>();
        ProfileValidator.ValidateVehicle(input, string.Empty, errors);
        if (errors.Count > 0)
        {
            return Result.Failure<VehicleDto>(ErrorKind.Validation, [.. errors]);
        }

        var driver = await DriverOwningAsync(userId, vehicleId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure<VehicleDto>(ErrorKind.NotFound, new AppError("vehicle.not_found"));
        }

        return await MutateAsync(() =>
        {
            driver.UpdateVehicle(vehicleId, input.LicensePlateNumber!, input.Model!, input.PassengerCapacity!.Value);
            return ToDto(driver.Vehicles.Single(v => v.Id == vehicleId));
        }, cancellationToken);
    }

    public async Task<Result> DeleteVehicleAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var driver = await DriverOwningAsync(userId, vehicleId, cancellationToken);
        if (driver is null)
        {
            return Result.Failure(ErrorKind.NotFound, new AppError("vehicle.not_found"));
        }

        var vehicle = driver.Vehicles.Single(v => v.Id == vehicleId);
        driver.RemoveVehicle(vehicleId);
        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ------------------------------------------------------------------ orchards

    public async Task<IReadOnlyList<OrchardDto>> GetOrchardsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var statusCodes = await StatusCodesAsync(cancellationToken);
        var orchards = await db.Orchards.AsNoTracking()
            .Where(o => db.OrchardOwnerProfiles.Any(p => p.Id == o.OwnerProfileId && p.UserId == userId))
            .OrderBy(o => o.CreatedAt).ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);
        return orchards.Select(o => ToDto(o, statusCodes)).ToList();
    }

    public async Task<OrchardDto?> GetOrchardAsync(Guid userId, Guid orchardId, CancellationToken cancellationToken)
    {
        var orchard = await db.Orchards.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orchardId && db.OrchardOwnerProfiles.Any(p => p.Id == o.OwnerProfileId && p.UserId == userId), cancellationToken);
        return orchard is null ? null : ToDto(orchard, await StatusCodesAsync(cancellationToken));
    }

    public async Task<Result<OrchardDto>> AddOrchardAsync(Guid userId, OrchardInput input, CancellationToken cancellationToken)
    {
        var errors = await ValidateOrchardAsync(input, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.Failure<OrchardDto>(ErrorKind.Validation, [.. errors]);
        }

        var owner = await db.OrchardOwnerProfiles.Include(o => o.Orchards).FirstOrDefaultAsync(o => o.UserId == userId && o.IsActive, cancellationToken);
        if (owner is null)
        {
            return Result.Failure<OrchardDto>(ErrorKind.NotFound, new AppError("profile.not_registered"));
        }

        var statusCodes = await StatusCodesAsync(cancellationToken);
        return await MutateAsync(() =>
        {
            var orchard = owner.AddOrchard(ToDetails(input));
            db.Orchards.Add(orchard);
            return ToDto(orchard, statusCodes);
        }, cancellationToken);
    }

    public async Task<Result<OrchardDto>> UpdateOrchardAsync(Guid userId, Guid orchardId, OrchardInput input, CancellationToken cancellationToken)
    {
        var errors = await ValidateOrchardAsync(input, cancellationToken);
        if (errors.Count > 0)
        {
            return Result.Failure<OrchardDto>(ErrorKind.Validation, [.. errors]);
        }

        var orchard = await db.Orchards
            .FirstOrDefaultAsync(o => o.Id == orchardId && db.OrchardOwnerProfiles.Any(p => p.Id == o.OwnerProfileId && p.UserId == userId), cancellationToken);
        if (orchard is null)
        {
            return Result.Failure<OrchardDto>(ErrorKind.NotFound, new AppError("orchard.not_found"));
        }

        var statusCodes = await StatusCodesAsync(cancellationToken);
        return await MutateAsync(() =>
        {
            orchard.Update(ToDetails(input));
            return ToDto(orchard, statusCodes);
        }, cancellationToken);
    }

    public async Task<Result> DeleteOrchardAsync(Guid userId, Guid orchardId, CancellationToken cancellationToken)
    {
        var owner = await db.OrchardOwnerProfiles.Include(o => o.Orchards)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Orchards.Any(x => x.Id == orchardId), cancellationToken);
        if (owner is null)
        {
            return Result.Failure(ErrorKind.NotFound, new AppError("orchard.not_found"));
        }

        var orchard = owner.Orchards.Single(o => o.Id == orchardId);
        owner.RemoveOrchard(orchardId);
        db.Orchards.Remove(orchard);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Creates or updates the user's personal details (not saved yet). Checks one-person-one-account.</summary>
    private async Task<Result<PersonalDetails>> ApplyPersonalDetailsAsync(
        Guid userId, PersonalDetails? personal, PersonalDetailsInput input, string prefix, CancellationToken cancellationToken)
    {
        ProtectedValue? pin = null;
        if (!string.IsNullOrWhiteSpace(input.PersonalIdNumber))
        {
            pin = protector.Protect(input.PersonalIdNumber, SensitiveDataKind.PersonalIdNumber, withBlindIndex: true);
            var takenByOther = await db.PersonalDetails.AnyAsync(p => p.PersonalIdNumberHash == pin.BlindIndex && p.UserId != userId, cancellationToken)
                || await db.LinkedPersons.AnyAsync(l => l.PersonalIdNumberHash == pin.BlindIndex, cancellationToken);
            if (takenByOther)
            {
                return Result.Failure<PersonalDetails>(ErrorKind.Conflict, new AppError("profile.personal_id_taken", prefix + "personalIdNumber"));
            }
        }

        // The holder's ID is encrypted like the user's own, but has no uniqueness index: one person may hold
        // the bank account of several workers (e.g. a parent).
        ProtectedValue? holderId = null;
        if (input.BankAccountOwnedByOther && !string.IsNullOrWhiteSpace(input.AccountHolderPersonalIdNumber))
        {
            var ownIdHash = pin?.BlindIndex ?? personal?.PersonalIdNumberHash;
            if (protector.ComputeBlindIndex(input.AccountHolderPersonalIdNumber, SensitiveDataKind.PersonalIdNumber) == ownIdHash)
            {
                return Result.Failure<PersonalDetails>(ErrorKind.Validation, new AppError("validation.holder_is_self", prefix + "accountHolderPersonalIdNumber"));
            }

            holderId = protector.Protect(input.AccountHolderPersonalIdNumber, SensitiveDataKind.PersonalIdNumber, withBlindIndex: false);
        }

        var alternativePhone = PhoneNumberNormalizer.TryNormalize(input.AlternativePhoneNumber, out var normalizedPhone) ? normalizedPhone : null;

        var iban = string.IsNullOrWhiteSpace(input.BankAccountNumber)
            ? null
            : protector.Protect(input.BankAccountNumber, SensitiveDataKind.BankAccountNumber, withBlindIndex: false);
        var location = new GeoPoint(input.Latitude!.Value, input.Longitude!.Value);

        if (personal is null)
        {
            personal = new PersonalDetails(userId, input.FirstName!, input.LastName!, pin!, iban!, input.LegalAddress!, input.ActualAddress!, location);
            db.PersonalDetails.Add(personal);
        }
        else
        {
            personal.Update(input.FirstName!, input.LastName!, input.LegalAddress!, input.ActualAddress!, location);
            if (pin is not null)
            {
                personal.SetPersonalIdNumber(pin);
            }

            if (iban is not null)
            {
                personal.SetBankAccountNumber(iban);
            }
        }

        if (input.BankAccountOwnedByOther)
        {
            personal.SetBankAccountHolder(input.AccountHolderFirstName!, input.AccountHolderLastName!, holderId);
        }
        else
        {
            personal.SetBankAccountOwnedBySelf();
        }

        personal.SetAlternativePhoneNumber(alternativePhone);
        return Result.Success(personal);
    }

    private async Task<List<AppError>> ValidateOrchardAsync(OrchardInput input, CancellationToken cancellationToken)
    {
        var errors = new List<AppError>();
        var statuses = await ActiveStatusCodesAsync(cancellationToken);
        ProfileValidator.ValidateOrchard(input, StatusCode(input?.LegalEntityStatusId, statuses), string.Empty, errors);
        return errors;
    }

    private async Task<DriverProfile?> DriverOwningAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken) =>
        await db.DriverProfiles.Include(d => d.Vehicles)
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Vehicles.Any(v => v.Id == vehicleId), cancellationToken);

    /// <summary>Runs a domain change and saves it, turning rule violations and constraint conflicts into results.</summary>
    private async Task<Result<T>> MutateAsync<T>(Func<T> change, CancellationToken cancellationToken)
    {
        T value;
        try
        {
            value = change();
        }
        catch (DomainException ex)
        {
            var kind = ex.Code.EndsWith("duplicate_plate", StringComparison.Ordinal) ? ErrorKind.Conflict : ErrorKind.Validation;
            var field = ex.Code.EndsWith("duplicate_plate", StringComparison.Ordinal) ? "licensePlateNumber" : null;
            return Result.Failure<T>(kind, new AppError(ex.Code, field));
        }

        var conflict = await SaveAsync(cancellationToken);
        return conflict is null ? Result.Success(value) : Result.Failure<T>(ErrorKind.Conflict, conflict);
    }

    /// <summary>Saves; a unique-index race (two requests at once) becomes a conflict instead of a 500.</summary>
    private async Task<AppError?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException)
        {
            return new AppError("profile.conflict");
        }
    }

    private static T Activate<T>(T? profile, Func<T> create) where T : RoleProfile
    {
        if (profile is null)
        {
            return create();
        }

        profile.Reactivate();
        return profile;
    }

    private async Task<Dictionary<int, string>> ActiveStatusCodesAsync(CancellationToken cancellationToken) =>
        await db.LegalEntityStatuses.AsNoTracking().Where(s => s.IsActive).ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);

    private async Task<Dictionary<int, string>> StatusCodesAsync(CancellationToken cancellationToken) =>
        await db.LegalEntityStatuses.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);

    private static string? StatusCode(int? id, Dictionary<int, string> statuses) =>
        id is int value && statuses.TryGetValue(value, out var code) ? code : null;

    private static OrchardDetails ToDetails(OrchardInput o) => new(
        o.LegalEntityStatusId!.Value,
        o.Name!,
        GeorgianIdentifiers.DigitsOnly(o.IdentificationNumber),
        o.Address!,
        new GeoPoint(o.Latitude!.Value, o.Longitude!.Value),
        o.AreaHectares!.Value);

    private static CategoryStatusDto Status(RoleProfile? profile) =>
        new(profile is not null, profile?.IsActive == true, profile?.CreatedAt);

    private static PersonalDetailsDto ToDto(PersonalDetails p) => new(
        p.FirstName, p.LastName, p.PersonalIdNumberLast4, p.BankAccountLast4, p.LegalAddress, p.ActualAddress,
        p.Location.Latitude, p.Location.Longitude, p.AlternativePhoneNumber, p.BankAccountOwnedByOther,
        p.AccountHolderFirstName, p.AccountHolderLastName, p.AccountHolderPersonalIdLast4, p.UpdatedAt);

    private static VehicleDto ToDto(Vehicle v) => new(v.Id, v.LicensePlateNumber, v.Model, v.PassengerCapacity);

    private static OrchardDto ToDto(Orchard o, Dictionary<int, string> statusCodes) => new(
        o.Id, o.LegalEntityStatusId, statusCodes.GetValueOrDefault(o.LegalEntityStatusId, "other"), o.Name,
        o.IdentificationNumber, o.Address, o.Location.Latitude, o.Location.Longitude, o.AreaHectares);
}
