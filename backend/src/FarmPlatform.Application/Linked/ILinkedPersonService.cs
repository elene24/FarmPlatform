using FarmPlatform.Application.Common.Results;

namespace FarmPlatform.Application.Linked;

/// <summary>
/// Representatives managing pickers who have no account, and the consent of those pickers.
/// Representative methods take the signed-in user's id; the public consent methods take the secret link token.
/// </summary>
public interface ILinkedPersonService
{
    Task<IReadOnlyList<LinkedPersonDto>> ListAsync(Guid representativeId, CancellationToken cancellationToken);

    Task<LinkedPersonDto?> GetAsync(Guid representativeId, Guid id, CancellationToken cancellationToken);

    /// <summary>Adds a linked person and sends them the consent SMS.</summary>
    Task<Result<LinkedPersonDto>> CreateAsync(Guid representativeId, LinkedPersonInput input, CancellationToken cancellationToken);

    /// <summary>Updates details. Changing the phone number or payment arrangement asks for consent again.</summary>
    Task<Result<LinkedPersonDto>> UpdateAsync(Guid representativeId, Guid id, LinkedPersonInput input, CancellationToken cancellationToken);

    /// <summary>Ends the representation and deletes the person's data; the person is told by SMS.</summary>
    Task<Result> DeleteAsync(Guid representativeId, Guid id, CancellationToken cancellationToken);

    Task<Result<LinkedPersonDto>> ResendConsentAsync(Guid representativeId, Guid id, CancellationToken cancellationToken);

    /// <summary>The representative enters the code the person read out from their SMS.</summary>
    Task<Result<LinkedPersonDto>> ConfirmWithCodeAsync(Guid representativeId, Guid id, string? code, CancellationToken cancellationToken);

    Task<ConsentRequestDto?> GetConsentRequestAsync(string token, CancellationToken cancellationToken);

    Task<Result<ConsentRequestDto>> RespondAsync(string token, ConsentResponse response, CancellationToken cancellationToken);
}
