using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Shared.Common;
using eImovina.Shared.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LocationsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<PagedResult<LocationListItemDto>>> GetLocations([FromQuery] LocationQuery query, CancellationToken ct)
    {
        var filtered =
            from l in _db.Locations.AsNoTracking()
            join lt in _db.LocationTypes.AsNoTracking() on l.LocationTypeId equals lt.Id
            select new { Location = l, LocationTypeName = lt.Name };

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x => EF.Functions.Like(x.Location.Name, $"%{text}%") || EF.Functions.Like(x.Location.Code, $"%{text}%"));
        }

        if (query.LocationTypeId is int locationTypeId)
        {
            filtered = filtered.Where(x => x.Location.LocationTypeId == locationTypeId);
        }

        if (query.IsActive is bool isActive)
        {
            filtered = filtered.Where(x => x.Location.IsActive == isActive);
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "code" => descending ? filtered.OrderByDescending(x => x.Location.Code) : filtered.OrderBy(x => x.Location.Code),
            "locationtype" => descending ? filtered.OrderByDescending(x => x.LocationTypeName) : filtered.OrderBy(x => x.LocationTypeName),
            "isactive" => descending ? filtered.OrderByDescending(x => x.Location.IsActive) : filtered.OrderBy(x => x.Location.IsActive),
            _ => descending ? filtered.OrderByDescending(x => x.Location.Name) : filtered.OrderBy(x => x.Location.Name),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LocationListItemDto(x.Location.Id, x.Location.Name, x.Location.Code, x.LocationTypeName, x.Location.Address, x.Location.IsActive))
            .ToListAsync(ct);

        return Ok(new PagedResult<LocationListItemDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<LocationDetailDto>> GetLocation(int id, CancellationToken ct)
    {
        var dto = await (
            from l in _db.Locations.AsNoTracking()
            join lt in _db.LocationTypes.AsNoTracking() on l.LocationTypeId equals lt.Id
            where l.Id == id
            select new LocationDetailDto(l.Id, l.Name, l.Code, l.LocationTypeId, lt.Name, l.Address, l.IsActive, l.CreatedAtUtc)
        ).SingleOrDefaultAsync(ct);

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<LocationDetailDto>> CreateLocation([FromBody] LocationCreateDto request, CancellationToken ct)
    {
        if (await _db.Locations.AnyAsync(l => l.Code == request.Code, ct))
        {
            return Problem(detail: $"Lokacija s oznakom '{request.Code}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!await _db.LocationTypes.AnyAsync(lt => lt.Id == request.LocationTypeId, ct))
        {
            return Problem(detail: "Odabrani tip lokacije ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        var location = new Location
        {
            Name = request.Name,
            Code = request.Code,
            LocationTypeId = request.LocationTypeId,
            Address = request.Address,
        };

        _db.Locations.Add(location);
        await _db.SaveChangesAsync(ct);

        var locationTypeName = await _db.LocationTypes.Where(lt => lt.Id == location.LocationTypeId).Select(lt => lt.Name).SingleAsync(ct);
        var dto = new LocationDetailDto(location.Id, location.Name, location.Code, location.LocationTypeId, locationTypeName, location.Address, location.IsActive, location.CreatedAtUtc);

        return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationUpdateDto request, CancellationToken ct)
    {
        var location = await _db.Locations.FindAsync([id], ct);
        if (location is null)
        {
            return NotFound();
        }

        if (await _db.Locations.AnyAsync(l => l.Id != id && l.Code == request.Code, ct))
        {
            return Problem(detail: $"Lokacija s oznakom '{request.Code}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!await _db.LocationTypes.AnyAsync(lt => lt.Id == request.LocationTypeId, ct))
        {
            return Problem(detail: "Odabrani tip lokacije ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!request.IsActive && await IsLocationReferencedAsync(id, ct))
        {
            return Problem(
                detail: "Lokacija se ne može deaktivirati jer je trenutačno povezana s opremom ili inventurom u tijeku.",
                statusCode: StatusCodes.Status409Conflict);
        }

        location.Name = request.Name;
        location.Code = request.Code;
        location.LocationTypeId = request.LocationTypeId;
        location.Address = request.Address;
        location.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Three-way: 404 if missing; 409 if something currently blocks deactivation; otherwise either
    // a real physical delete (location has never been referenced by anything, ever - nothing to
    // lose) or the usual soft-deactivate (some history exists, e.g. an employee, but nothing
    // currently blocks it). The DB's own [delete: restrict] FKs (docs/database.dbml) back this up
    // regardless - a wrong hard-delete attempt would fail there too.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<LocationDeleteResultDto>> RemoveLocation(int id, CancellationToken ct)
    {
        var location = await _db.Locations.FindAsync([id], ct);
        if (location is null)
        {
            return NotFound();
        }

        if (await IsLocationReferencedAsync(id, ct))
        {
            return Problem(
                detail: "Lokacija se ne može deaktivirati jer je trenutačno povezana s opremom ili inventurom u tijeku.",
                statusCode: StatusCodes.Status409Conflict);
        }

        if (!await HasAnyLocationHistoryAsync(id, ct))
        {
            _db.Locations.Remove(location);
            await _db.SaveChangesAsync(ct);
            return Ok(new LocationDeleteResultDto(HardDeleted: true));
        }

        location.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return Ok(new LocationDeleteResultDto(HardDeleted: false));
    }

    // Shared by PUT (toggling "Aktivna" off) and DELETE (the row shortcut) so both enforce the
    // identical rule. Employees are deliberately NOT checked here - the mandatory app scope has no
    // employee-relocation workflow, so blocking on staff assignment would make deactivation
    // permanently impossible for any real, staffed location. Only live inventories (Otvorena/U
    // tijeku/Zavrsena) block; a Nacrt or Zakljucana inventory doesn't, since history is preserved
    // via snapshot columns regardless of the location's IsActive flag.
    private async Task<bool> IsLocationReferencedAsync(int id, CancellationToken ct)
    {
        var liveInventoryStatusIds = new[] { 2, 3, 4 }; // Otvorena, U tijeku, Završena
        return await _db.Equipment.AnyAsync(e => e.CurrentLocationId == id, ct)
            || await _db.Inventories.AnyAsync(i => i.LocationId == id && liveInventoryStatusIds.Contains(i.InventoryStatusId), ct);
    }

    // Broader than IsLocationReferencedAsync: "has anything ever pointed here", not just "is
    // anything currently blocking deactivation". Used only to decide hard-delete eligibility -
    // includes employees and every inventory status, matching every [delete: restrict] FK the DB
    // itself enforces against Locations.
    private async Task<bool> HasAnyLocationHistoryAsync(int id, CancellationToken ct)
    {
        return await _db.Equipment.AnyAsync(e => e.CurrentLocationId == id, ct)
            || await _db.Employees.AnyAsync(e => e.LocationId == id, ct)
            || await _db.Inventories.AnyAsync(i => i.LocationId == id, ct)
            || await _db.InventoryItems.AnyAsync(ii => ii.ExpectedLocationId == id || ii.FoundLocationId == id, ct);
    }
}
