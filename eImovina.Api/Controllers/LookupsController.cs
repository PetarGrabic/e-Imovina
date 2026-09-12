using eImovina.Api.Data;
using eImovina.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/lookups")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LookupsController(AppDbContext db)
    {
        _db = db;
    }

    // Single dispatch endpoint for every lookup used across the app's forms - the UI always
    // shows Name and stores/sends Id. AppRoles has no IsActive column (see AppRole entity), so
    // it's the one branch with no Where(x => x.IsActive) filter.
    [HttpGet("{name}")]
    public async Task<ActionResult<List<LookupDto>>> GetLookup(string name, CancellationToken ct)
    {
        IQueryable<LookupDto>? query = name.ToLowerInvariant() switch
        {
            "locationtypes" => _db.LocationTypes.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "equipmentcategories" => _db.EquipmentCategories.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "equipmentstatuses" => _db.EquipmentStatuses.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "assignmentstatuses" => _db.AssignmentStatuses.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "inventorystatuses" => _db.InventoryStatuses.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "requeststatuses" => _db.RequestStatuses.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "writeoffrequeststatuses" => _db.WriteOffRequestStatuses.AsNoTracking().Where(x => x.IsActive).Select(x => new LookupDto(x.Id, x.Name)),
            "roles" => _db.AppRoles.AsNoTracking().Select(x => new LookupDto(x.Id, x.Name)),
            "employees" => _db.Employees.AsNoTracking().Where(e => e.IsActive).Select(e => new LookupDto(e.Id, e.FirstName + " " + e.LastName)),
            _ => null,
        };

        if (query is null)
        {
            return Problem(detail: $"Nepoznat šifrarnik '{name}'.", statusCode: StatusCodes.Status404NotFound);
        }

        // Ordering by the projected record's Id doesn't translate to SQL on the Sqlite provider -
        // these lookup tables are tiny, so order client-side after materializing instead.
        var items = (await query.ToListAsync(ct)).OrderBy(x => x.Id).ToList();
        return Ok(items);
    }
}
