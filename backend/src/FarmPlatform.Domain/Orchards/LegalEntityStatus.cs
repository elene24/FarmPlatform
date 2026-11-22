namespace FarmPlatform.Domain.Orchards;

/// <summary>
/// Configurable lookup of legal forms (Individual Entrepreneur, LLC, ...). Kept in a table rather
/// than an enum so options can be added, renamed or hidden without a code deployment.
/// </summary>
public sealed class LegalEntityStatus
{
    private LegalEntityStatus() { } // EF Core

    public LegalEntityStatus(int id, string code, string nameKa, string nameEn, int sortOrder)
    {
        Id = id;
        Code = code;
        NameKa = nameKa;
        NameEn = nameEn;
        SortOrder = sortOrder;
        IsActive = true;
    }

    public int Id { get; private set; }

    /// <summary>Stable identifier used by code and the frontend, e.g. "llc".</summary>
    public string Code { get; private set; } = string.Empty;

    public string NameKa { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    /// <summary>Inactive options are hidden from new registrations but kept for existing orchards.</summary>
    public bool IsActive { get; private set; }
}
