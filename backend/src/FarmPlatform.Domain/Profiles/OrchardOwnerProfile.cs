using FarmPlatform.Domain.Common;
using FarmPlatform.Domain.Orchards;

namespace FarmPlatform.Domain.Profiles;

/// <summary>The Orchard owner category. Owns one or more orchards.</summary>
public sealed class OrchardOwnerProfile : RoleProfile
{
    public const int MaxOrchards = 50;

    private readonly List<Orchard> _orchards = [];

    private OrchardOwnerProfile() { } // EF Core

    public OrchardOwnerProfile(Guid userId) : base(userId) { }

    public override ProfileCategory Category => ProfileCategory.OrchardOwner;

    public IReadOnlyCollection<Orchard> Orchards => _orchards.AsReadOnly();

    public Orchard AddOrchard(OrchardDetails details)
    {
        if (_orchards.Count >= MaxOrchards)
        {
            throw new DomainException("orchard.limit_reached", $"An owner can register at most {MaxOrchards} orchards.");
        }

        var orchard = new Orchard(Id, details);
        _orchards.Add(orchard);
        return orchard;
    }

    public void RemoveOrchard(Guid orchardId)
    {
        var orchard = _orchards.FirstOrDefault(o => o.Id == orchardId)
            ?? throw new DomainException("orchard.not_found", "Orchard not found.");
        _orchards.Remove(orchard);
    }
}
