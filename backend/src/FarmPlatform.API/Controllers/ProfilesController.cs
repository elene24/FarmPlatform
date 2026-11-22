using FarmPlatform.Api.Errors;
using FarmPlatform.Application.Common.Security;
using FarmPlatform.Application.Profiles;
using FarmPlatform.Domain.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmPlatform.Api.Controllers;

/// <summary>
/// Registration categories and the shared personal details. All data is scoped to the signed-in user;
/// there are no ids in these routes, so one user can never address another user's profile.
/// </summary>
[ApiController]
[Authorize]
public sealed class ProfilesController(IProfileService profiles, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Dashboard summary: personal details (masked), each category's status, vehicles and orchards.</summary>
    [HttpGet("/api/v1/profiles")]
    public async Task<ActionResult<ProfilesSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await profiles.GetSummaryAsync(currentUser.UserId, cancellationToken));

    /// <summary>Registration wizard submit: any combination of categories with their data, saved atomically.</summary>
    [HttpPost("/api/v1/profiles/registration")]
    public Task<ActionResult<ProfilesSummaryDto>> Register(CategoryRegistrationCommand command, CancellationToken cancellationToken) =>
        RegisterAsync(command, cancellationToken);

    [HttpPost("/api/v1/profiles/picker")]
    public Task<ActionResult<ProfilesSummaryDto>> RegisterPicker(PickerRegistrationRequest? request, CancellationToken cancellationToken) =>
        RegisterAsync(new CategoryRegistrationCommand(true, false, false, request?.PersonalDetails, null, null), cancellationToken);

    [HttpPost("/api/v1/profiles/driver")]
    public Task<ActionResult<ProfilesSummaryDto>> RegisterDriver(DriverRegistrationRequest? request, CancellationToken cancellationToken) =>
        RegisterAsync(new CategoryRegistrationCommand(false, true, false, request?.PersonalDetails, request?.Vehicles, null), cancellationToken);

    [HttpPost("/api/v1/profiles/orchard")]
    public Task<ActionResult<ProfilesSummaryDto>> RegisterOrchardOwner(OrchardOwnerRegistrationRequest? request, CancellationToken cancellationToken) =>
        RegisterAsync(new CategoryRegistrationCommand(false, false, true, null, null, request?.Orchards), cancellationToken);

    // ---- Personal details (shared by Picker and Driver) ----

    [HttpGet("/api/v1/personal-details/me")]
    public async Task<ActionResult<PersonalDetailsDto>> GetPersonalDetails(CancellationToken cancellationToken)
    {
        var details = await profiles.GetPersonalDetailsAsync(currentUser.UserId, cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    /// <summary>
    /// Creates or updates personal details. Leave personalIdNumber / bankAccountNumber empty to keep the stored values.
    /// PUT /pickers/me and /drivers/me are aliases: the personal information is shared between both categories.
    /// </summary>
    [HttpPut("/api/v1/personal-details/me")]
    [HttpPut("/api/v1/pickers/me")]
    [HttpPut("/api/v1/drivers/me")]
    public async Task<ActionResult<PersonalDetailsDto>> SavePersonalDetails(PersonalDetailsInput input, CancellationToken cancellationToken)
    {
        var result = await profiles.SavePersonalDetailsAsync(currentUser.UserId, input, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    [HttpGet("/api/v1/pickers/me")]
    public async Task<ActionResult<PickerSummaryResponse>> GetPicker(CancellationToken cancellationToken)
    {
        var summary = await profiles.GetSummaryAsync(currentUser.UserId, cancellationToken);
        return summary.Picker.Status.Registered ? Ok(new PickerSummaryResponse(summary.Picker.Status, summary.PersonalDetails)) : NotFound();
    }

    [HttpGet("/api/v1/drivers/me")]
    public async Task<ActionResult<DriverSummaryDto>> GetDriver(CancellationToken cancellationToken)
    {
        var summary = await profiles.GetSummaryAsync(currentUser.UserId, cancellationToken);
        return summary.Driver.Status.Registered ? Ok(summary.Driver) : NotFound();
    }

    [HttpGet("/api/v1/orchard-owners/me")]
    public async Task<ActionResult<OrchardOwnerSummaryDto>> GetOrchardOwner(CancellationToken cancellationToken)
    {
        var summary = await profiles.GetSummaryAsync(currentUser.UserId, cancellationToken);
        return summary.OrchardOwner.Status.Registered ? Ok(summary.OrchardOwner) : NotFound();
    }

    // ---- Deactivate a category (the account and its data are kept) ----

    [HttpDelete("/api/v1/pickers/me")]
    public Task<IActionResult> DeactivatePicker(CancellationToken cancellationToken) => DeactivateAsync(ProfileCategory.Picker, cancellationToken);

    [HttpDelete("/api/v1/drivers/me")]
    public Task<IActionResult> DeactivateDriver(CancellationToken cancellationToken) => DeactivateAsync(ProfileCategory.Driver, cancellationToken);

    [HttpDelete("/api/v1/orchard-owners/me")]
    public Task<IActionResult> DeactivateOrchardOwner(CancellationToken cancellationToken) => DeactivateAsync(ProfileCategory.OrchardOwner, cancellationToken);

    private async Task<ActionResult<ProfilesSummaryDto>> RegisterAsync(CategoryRegistrationCommand command, CancellationToken cancellationToken)
    {
        var result = await profiles.RegisterCategoriesAsync(currentUser.UserId, command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    private async Task<IActionResult> DeactivateAsync(ProfileCategory category, CancellationToken cancellationToken)
    {
        var result = await profiles.DeactivateCategoryAsync(currentUser.UserId, category, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }
}

public sealed record PickerSummaryResponse(CategoryStatusDto Status, PersonalDetailsDto? PersonalDetails);

public sealed record PickerRegistrationRequest(PersonalDetailsInput? PersonalDetails);

public sealed record DriverRegistrationRequest(PersonalDetailsInput? PersonalDetails, IReadOnlyList<VehicleInput>? Vehicles);

public sealed record OrchardOwnerRegistrationRequest(IReadOnlyList<OrchardInput>? Orchards);
