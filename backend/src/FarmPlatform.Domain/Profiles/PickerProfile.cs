namespace FarmPlatform.Domain.Profiles;

/// <summary>
/// The Picker category. Personal data lives in <see cref="PersonalDetails"/>; this row records
/// the category itself and is where picker-specific data (skills, availability, ...) will go later.
/// </summary>
public sealed class PickerProfile : RoleProfile
{
    private PickerProfile() { } // EF Core

    public PickerProfile(Guid userId) : base(userId) { }

    public override ProfileCategory Category => ProfileCategory.Picker;
}
