using FarmPlatform.Domain.Orchards;

namespace FarmPlatform.Domain.Profiles;

/// <summary>The Orchard owner category. Owns one or more orchards.</summary>
public sealed class OrchardOwnerProfile : RoleProfile
{
    private readonly List<Orchard> _orchards = [];

    private OrchardOwnerProfile() { } // EF Core

    public OrchardOwnerProfile(Guid userId) : base(userId) { }

    public override ProfileCategory Category => ProfileCategory.OrchardOwner;

    public IReadOnlyCollection<Orchard> Orchards => _orchards.AsReadOnly();

    public Orchard AddOrchard(OrchardDetails details)
    {
        var orchard = new Orchard(Id, details);
        _orchards.Add(orchard);
        return orchard;
    }
}
