using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
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
    public async Task<ActionResult<List<LookupDto>>> GetLookup(string name, [FromQuery] int? locationId, [FromQuery] string? role, [FromQuery] bool? unlinkedOnly, CancellationToken ct)
    {
        // Only used by the "employees" branch below. Built as a plain C# branch (not an
        // `employeeIdsWithRole == null || ...` check inside the query itself) because EF Core
        // cannot translate a null-check against an IQueryable local variable that's part of the
        // same expression tree - it tries to push the comparison into SQL and throws. Resolves to
        // the set of Employee ids whose linked AppUser (AppUser.EmployeeId is nullable; not every
        // employee has a login) holds the given role name. AppUserRole has no navigation
        // properties (raw FK ints only), so this is an explicit join, same pattern AuthController
        // uses to build a user's own JWT role claims.
        IQueryable<Employee> EmployeesQuery()
        {
            var employees = _db.Employees.AsNoTracking().Where(e => e.IsActive && (locationId == null || e.LocationId == locationId));
            if (role is not null)
            {
                var employeeIdsWithRole =
                    from ur in _db.AppUserRoles.AsNoTracking()
                    join r in _db.AppRoles.AsNoTracking() on ur.AppRoleId equals r.Id
                    join u in _db.AppUsers.AsNoTracking() on ur.AppUserId equals u.Id
                    where r.Name == role && u.EmployeeId != null
                    select u.EmployeeId!.Value;

                employees = employees.Where(e => employeeIdsWithRole.Contains(e.Id));
            }

            // Users.razor's employee-link picker (Section 15) - an employee already linked to an
            // AppUser account shouldn't be offered as a choice the API would just reject with a
            // 409 for the unique AppUsers.EmployeeId index. Same "don't let the UI offer what the
            // API will reject" precedent as EquipmentQuery.ExcludeStatusId.
            if (unlinkedOnly == true)
            {
                var linkedEmployeeIds = _db.AppUsers.AsNoTracking().Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value);
                employees = employees.Where(e => !linkedEmployeeIds.Contains(e.Id));
            }

            return employees;
        }

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
            // Optional ?locationId= and ?role= filters (Section 11's CreateInventoryDialog / the
            // Inventories list's "Odgovorna osoba" filter) - every existing caller that omits both
            // keeps today's unfiltered "all active employees" behavior.
            "employees" => EmployeesQuery().Select(e => new LookupDto(e.Id, e.FirstName + " " + e.LastName)),
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
