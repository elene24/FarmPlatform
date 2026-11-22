using System.Text.RegularExpressions;
using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Results;
using FarmPlatform.Domain.Orchards;
using FarmPlatform.Domain.Profiles;
using FarmPlatform.Domain.Vehicles;

namespace FarmPlatform.Application.Profiles;

/// <summary>
/// Validation for profile inputs. Field names are the frontend form paths ("firstName", "vehicles.0.model"),
/// so errors land next to the right input. Mirrors frontend/src/features/profiles/validation.ts.
/// </summary>
public static partial class ProfileValidator
{
    public const decimal MaxAreaHectares = Orchard.MaxAreaHectares;

    /// <param name="hasStoredHolderId">The account holder's personal ID is already stored (so it may be omitted on update).</param>
    public static void ValidatePersonalDetails(PersonalDetailsInput? input, bool isNew, bool hasStoredHolderId, string prefix, List<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (input is null)
        {
            errors.Add(new AppError("profile.personal_details_required", prefix.TrimEnd('.')));
            return;
        }

        ValidateName(input.FirstName, prefix + "firstName", errors);
        ValidateName(input.LastName, prefix + "lastName", errors);

        if (string.IsNullOrWhiteSpace(input.PersonalIdNumber))
        {
            if (isNew)
            {
                errors.Add(new AppError("validation.required", prefix + "personalIdNumber"));
            }
        }
        else if (!GeorgianIdentifiers.IsPersonalNumber(input.PersonalIdNumber))
        {
            errors.Add(new AppError("validation.personal_id_invalid", prefix + "personalIdNumber"));
        }

        if (string.IsNullOrWhiteSpace(input.BankAccountNumber))
        {
            if (isNew)
            {
                errors.Add(new AppError("validation.required", prefix + "bankAccountNumber"));
            }
        }
        else if (!GeorgianIdentifiers.IsValidIban(input.BankAccountNumber))
        {
            errors.Add(new AppError("validation.iban_invalid", prefix + "bankAccountNumber"));
        }

        if (input.BankAccountOwnedByOther)
        {
            ValidateName(input.AccountHolderFirstName, prefix + "accountHolderFirstName", errors);
            ValidateName(input.AccountHolderLastName, prefix + "accountHolderLastName", errors);

            if (string.IsNullOrWhiteSpace(input.AccountHolderPersonalIdNumber))
            {
                if (!hasStoredHolderId)
                {
                    errors.Add(new AppError("validation.required", prefix + "accountHolderPersonalIdNumber"));
                }
            }
            else if (!GeorgianIdentifiers.IsPersonalNumber(input.AccountHolderPersonalIdNumber))
            {
                errors.Add(new AppError("validation.personal_id_invalid", prefix + "accountHolderPersonalIdNumber"));
            }
            else if (!string.IsNullOrWhiteSpace(input.PersonalIdNumber)
                && GeorgianIdentifiers.DigitsOnly(input.PersonalIdNumber) == GeorgianIdentifiers.DigitsOnly(input.AccountHolderPersonalIdNumber))
            {
                // Same person: the box "does not belong to me" was ticked by mistake.
                errors.Add(new AppError("validation.holder_is_self", prefix + "accountHolderPersonalIdNumber"));
            }
        }

        if (!string.IsNullOrWhiteSpace(input.AlternativePhoneNumber) && !PhoneNumberNormalizer.TryNormalize(input.AlternativePhoneNumber, out _))
        {
            errors.Add(new AppError("validation.phone_invalid", prefix + "alternativePhoneNumber"));
        }

        ValidateText(input.LegalAddress, PersonalDetails.AddressMaxLength, prefix + "legalAddress", errors);
        ValidateText(input.ActualAddress, PersonalDetails.AddressMaxLength, prefix + "actualAddress", errors);
        ValidateLocation(input.Latitude, input.Longitude, prefix + "location", errors);
    }

    public static void ValidateVehicle(VehicleInput? input, string prefix, List<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (input is null)
        {
            errors.Add(new AppError("validation.required", prefix.TrimEnd('.')));
            return;
        }

        var plate = Vehicle.NormalizePlate(input.LicensePlateNumber);
        if (string.IsNullOrWhiteSpace(input.LicensePlateNumber))
        {
            errors.Add(new AppError("validation.required", prefix + "licensePlateNumber"));
        }
        else if (plate.Length is < 4 or > 10 || !plate.All(char.IsAsciiLetterOrDigit))
        {
            errors.Add(new AppError("validation.plate_invalid", prefix + "licensePlateNumber"));
        }

        ValidateText(input.Model, Vehicle.ModelMaxLength, prefix + "model", errors);

        if (input.PassengerCapacity is null)
        {
            errors.Add(new AppError("validation.required", prefix + "passengerCapacity"));
        }
        else if (input.PassengerCapacity is < Vehicle.MinPassengerCapacity or > Vehicle.MaxPassengerCapacity)
        {
            errors.Add(new AppError("validation.capacity_invalid", prefix + "passengerCapacity"));
        }
    }

    /// <param name="statusCode">Code of the selected legal entity status, or null if unknown / inactive.</param>
    public static void ValidateOrchard(OrchardInput? input, string? statusCode, string prefix, List<AppError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (input is null)
        {
            errors.Add(new AppError("validation.required", prefix.TrimEnd('.')));
            return;
        }

        if (input.LegalEntityStatusId is null)
        {
            errors.Add(new AppError("validation.required", prefix + "legalEntityStatusId"));
        }
        else if (statusCode is null)
        {
            errors.Add(new AppError("validation.legal_status_invalid", prefix + "legalEntityStatusId"));
        }

        ValidateText(input.Name, Orchard.NameMaxLength, prefix + "name", errors);

        if (string.IsNullOrWhiteSpace(input.IdentificationNumber))
        {
            errors.Add(new AppError("validation.required", prefix + "identificationNumber"));
        }
        else if (statusCode is not null && !IsValidIdentificationNumber(input.IdentificationNumber, statusCode))
        {
            errors.Add(new AppError(
                statusCode == "llc" ? "validation.company_id_invalid" : statusCode == "other" ? "validation.identification_number_invalid" : "validation.personal_id_invalid",
                prefix + "identificationNumber"));
        }

        ValidateText(input.Address, Orchard.AddressMaxLength, prefix + "address", errors);
        ValidateLocation(input.Latitude, input.Longitude, prefix + "location", errors);

        if (input.AreaHectares is null)
        {
            errors.Add(new AppError("validation.required", prefix + "areaHectares"));
        }
        else if (input.AreaHectares <= 0 || input.AreaHectares > MaxAreaHectares || decimal.Round(input.AreaHectares.Value, 4) != input.AreaHectares)
        {
            errors.Add(new AppError("validation.area_invalid", prefix + "areaHectares"));
        }
    }

    /// <summary>LLC: 9-digit company code. Individual entrepreneur / natural person: 11-digit personal number. Other: either.</summary>
    public static bool IsValidIdentificationNumber(string value, string statusCode) => statusCode switch
    {
        "llc" => GeorgianIdentifiers.IsCompanyId(value),
        "individual_entrepreneur" or "natural_person" => GeorgianIdentifiers.IsPersonalNumber(value),
        _ => GeorgianIdentifiers.IsCompanyId(value) || GeorgianIdentifiers.IsPersonalNumber(value),
    };

    private static void ValidateName(string? value, string field, List<AppError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new AppError("validation.required", field));
        }
        else if (value.Trim().Length > PersonalDetails.NameMaxLength || !NamePattern().IsMatch(value.Trim()))
        {
            errors.Add(new AppError("validation.name_invalid", field));
        }
    }

    private static void ValidateText(string? value, int maxLength, string field, List<AppError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new AppError("validation.required", field));
        }
        else if (value.Trim().Length > maxLength)
        {
            errors.Add(new AppError("validation.too_long", field));
        }
    }

    private static void ValidateLocation(decimal? latitude, decimal? longitude, string field, List<AppError> errors)
    {
        if (latitude is null || longitude is null)
        {
            errors.Add(new AppError("validation.location_required", field));
        }
        else if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            errors.Add(new AppError("validation.location_invalid", field));
        }
    }

    // Letters in any script (Georgian, Latin, ...), combining marks, spaces, hyphens, apostrophes and dots.
    [GeneratedRegex(@"^\p{L}[\p{L}\p{M} .'\-]*$")]
    private static partial Regex NamePattern();
}
