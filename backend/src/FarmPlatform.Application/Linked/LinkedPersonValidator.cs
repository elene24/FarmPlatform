using FarmPlatform.Application.Auth;
using FarmPlatform.Application.Common.Results;
using FarmPlatform.Application.Profiles;
using FarmPlatform.Domain.Profiles;

namespace FarmPlatform.Application.Linked;

/// <summary>Input rules for linked persons. Field names match the frontend form.</summary>
public static class LinkedPersonValidator
{
    public static List<AppError> Validate(LinkedPersonInput? input, bool isNew, bool hasStoredBankAccount)
    {
        var errors = new List<AppError>();
        if (input is null)
        {
            errors.Add(new AppError("validation.required", "body"));
            return errors;
        }

        ProfileValidator.ValidateName(input.FirstName, "firstName", errors);
        ProfileValidator.ValidateName(input.LastName, "lastName", errors);

        if (string.IsNullOrWhiteSpace(input.PersonalIdNumber))
        {
            if (isNew)
            {
                errors.Add(new AppError("validation.required", "personalIdNumber"));
            }
        }
        else if (!GeorgianIdentifiers.IsPersonalNumber(input.PersonalIdNumber))
        {
            errors.Add(new AppError("validation.personal_id_invalid", "personalIdNumber"));
        }

        if (string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            errors.Add(new AppError("validation.required", "phoneNumber"));
        }
        else if (!PhoneNumberNormalizer.TryNormalize(input.PhoneNumber, out var phone))
        {
            errors.Add(new AppError("validation.phone_invalid", "phoneNumber"));
        }
        else if (phone.StartsWith("+995", StringComparison.Ordinal) && phone[4] != '5')
        {
            // Consent is asked by SMS, so Georgian numbers must be mobile (5XX XX XX XX).
            errors.Add(new AppError("validation.phone_not_mobile", "phoneNumber"));
        }

        if (!input.PayToRepresentative)
        {
            if (string.IsNullOrWhiteSpace(input.BankAccountNumber))
            {
                if (isNew || !hasStoredBankAccount)
                {
                    errors.Add(new AppError("validation.required", "bankAccountNumber"));
                }
            }
            else if (!GeorgianIdentifiers.IsValidIban(input.BankAccountNumber))
            {
                errors.Add(new AppError("validation.iban_invalid", "bankAccountNumber"));
            }
        }

        ProfileValidator.ValidateText(input.LegalAddress, PersonalDetails.AddressMaxLength, "legalAddress", errors);
        ProfileValidator.ValidateText(input.ActualAddress, PersonalDetails.AddressMaxLength, "actualAddress", errors);
        ProfileValidator.ValidateLocation(input.Latitude, input.Longitude, "location", errors);
        return errors;
    }
}
