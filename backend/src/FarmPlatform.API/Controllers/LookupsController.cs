using FarmPlatform.Application.Common.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmPlatform.Api.Controllers;

/// <summary>Reference data for forms. Public and cacheable.</summary>
[ApiController]
[Route("api/v1/lookups")]
[AllowAnonymous]
public sealed class LookupsController(IAppDbContext db) : ControllerBase
{
    [HttpGet("legal-entity-statuses")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<IReadOnlyList<LegalEntityStatusResponse>>> GetLegalEntityStatuses(CancellationToken cancellationToken)
    {
        var items = await db.LegalEntityStatuses
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new LegalEntityStatusResponse(s.Id, s.Code, s.NameKa, s.NameEn))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}

public sealed record LegalEntityStatusResponse(int Id, string Code, string NameKa, string NameEn);
