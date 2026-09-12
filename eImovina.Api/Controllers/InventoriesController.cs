using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Api.Services;
using eImovina.Shared.Common;
using eImovina.Shared.Inventories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/inventories")]
public class InventoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public InventoriesController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private bool CallerIsManager() => _currentUser.IsInRole(RoleNames.Admin) || _currentUser.IsInRole(RoleNames.InventoryManager);

    private async Task<int?> GetCallerLocationIdAsync(CancellationToken ct)
    {
        if (_currentUser.EmployeeId is not int employeeId)
        {
            return null;
        }

        return await _db.Employees.Where(e => e.Id == employeeId).Select(e => (int?)e.LocationId).SingleOrDefaultAsync(ct);
    }

    // AppUserRole has no navigation properties (raw FK ints only) - explicit join, same pattern
    // AuthController uses to build a user's own JWT role claims. AppUser.EmployeeId is nullable
    // (not every employee has a login), which the join naturally excludes.
    private async Task<bool> CandidateHasLocationResponsibleRoleAsync(int employeeId, CancellationToken ct)
    {
        return await (
            from ur in _db.AppUserRoles
            join r in _db.AppRoles on ur.AppRoleId equals r.Id
            join u in _db.AppUsers on ur.AppUserId equals u.Id
            where r.Name == RoleNames.LocationResponsible && u.EmployeeId == employeeId
            select ur).AnyAsync(ct);
    }

    // Admin/InventoryManager are unrestricted. A LocationResponsible (or any other non-manager
    // LocationWork caller) may only act on their own Employee.LocationId, resolved fresh per
    // request since ICurrentUser carries no LocationId claim (Section 11 planning decision #1).
    private async Task<bool> CallerCanAccessLocationAsync(int locationId, CancellationToken ct)
    {
        if (CallerIsManager())
        {
            return true;
        }

        var callerLocationId = await GetCallerLocationIdAsync(ct);
        return callerLocationId == locationId;
    }

    [HttpGet]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<PagedResult<InventoryListDto>>> GetInventories([FromQuery] InventoryQuery query, CancellationToken ct)
    {
        var filtered =
            from i in _db.Inventories.AsNoTracking()
            join l in _db.Locations.AsNoTracking() on i.LocationId equals l.Id
            join emp in _db.Employees.AsNoTracking() on i.ResponsibleEmployeeId equals emp.Id
            join s in _db.InventoryStatuses.AsNoTracking() on i.InventoryStatusId equals s.Id
            select new { Inventory = i, LocationName = l.Name, ResponsibleName = emp.FirstName + " " + emp.LastName, StatusName = s.Name };

        if (CallerIsManager())
        {
            if (query.LocationId is int locationId)
            {
                filtered = filtered.Where(x => x.Inventory.LocationId == locationId);
            }
        }
        else
        {
            // Non-manager LocationWork callers (LocationResponsible) are force-scoped to their own
            // location - any client-sent LocationId is ignored, same spirit as GetMine's JWT-only
            // scoping (Section 10's AssignmentsController).
            var callerLocationId = await GetCallerLocationIdAsync(ct);
            filtered = filtered.Where(x => x.Inventory.LocationId == callerLocationId);
        }

        if (query.InventoryStatusId is int statusId)
        {
            filtered = filtered.Where(x => x.Inventory.InventoryStatusId == statusId);
        }

        if (query.ResponsibleEmployeeId is int responsibleEmployeeId)
        {
            filtered = filtered.Where(x => x.Inventory.ResponsibleEmployeeId == responsibleEmployeeId);
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "location" => descending ? filtered.OrderByDescending(x => x.LocationName) : filtered.OrderBy(x => x.LocationName),
            "status" => descending ? filtered.OrderByDescending(x => x.Inventory.InventoryStatusId) : filtered.OrderBy(x => x.Inventory.InventoryStatusId),
            "opened" => descending ? filtered.OrderByDescending(x => x.Inventory.OpenedAtUtc) : filtered.OrderBy(x => x.Inventory.OpenedAtUtc),
            // No explicit sort requested: most-recent-first is the natural default, regardless of
            // Dir - mirrors AssignmentsController.GetAssignments' default branch.
            _ => filtered.OrderByDescending(x => x.Inventory.CreatedAtUtc),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InventoryListDto(
                x.Inventory.Id, x.Inventory.LocationId, x.LocationName,
                x.Inventory.ResponsibleEmployeeId, x.ResponsibleName,
                x.Inventory.InventoryStatusId, x.StatusName,
                x.Inventory.OpenedAtUtc, x.Inventory.CompletedAtUtc, x.Inventory.LockedAtUtc,
                x.Inventory.CreatedAtUtc, x.Inventory.Note))
            .ToListAsync(ct);

        return Ok(new PagedResult<InventoryListDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<InventoryDetailDto>> GetInventoryById(int id, CancellationToken ct)
    {
        var inventory = await _db.Inventories.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        var locationName = await _db.Locations.Where(l => l.Id == inventory.LocationId).Select(l => l.Name).SingleAsync(ct);
        var responsibleName = await _db.Employees.Where(e => e.Id == inventory.ResponsibleEmployeeId).Select(e => e.FirstName + " " + e.LastName).SingleAsync(ct);
        var statusName = await _db.InventoryStatuses.Where(s => s.Id == inventory.InventoryStatusId).Select(s => s.Name).SingleAsync(ct);
        var createdByName = await _db.AppUsers.Where(u => u.Id == inventory.CreatedByUserId).Select(u => u.UserName).SingleAsync(ct);

        var itemsQuery = _db.InventoryItems.AsNoTracking().Where(ii => ii.InventoryId == id);
        var totalItemCount = await itemsQuery.CountAsync(ct);
        var processedItemCount = await itemsQuery.CountAsync(ii => ii.IsFound != null, ct);
        var foundCount = await itemsQuery.CountAsync(ii => ii.IsFound == true, ct);
        var missingCount = await itemsQuery.CountAsync(ii => ii.IsFound == false, ct);
        var damagedCount = await itemsQuery.CountAsync(ii => ii.IsDamaged == true, ct);
        var discrepancyCount = await itemsQuery.CountAsync(ii =>
            ii.IsFound == false || ii.IsDamaged == true || (ii.IsFound == true && ii.FoundLocationId != null && ii.FoundLocationId != ii.ExpectedLocationId), ct);

        var dto = new InventoryDetailDto(
            inventory.Id, inventory.LocationId, locationName,
            inventory.ResponsibleEmployeeId, responsibleName,
            inventory.InventoryStatusId, statusName,
            inventory.OpenedAtUtc, inventory.CompletedAtUtc, inventory.LockedAtUtc,
            inventory.CreatedByUserId, createdByName, inventory.CreatedAtUtc, inventory.Note,
            totalItemCount, processedItemCount, foundCount, missingCount, damagedCount, discrepancyCount);

        return Ok(dto);
    }

    [HttpGet("{id:int}/items")]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetInventoryItems(int id, [FromQuery] InventoryItemQuery query, CancellationToken ct)
    {
        var inventory = await _db.Inventories.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        var filtered =
            from ii in _db.InventoryItems.AsNoTracking()
            join expected in _db.Locations.AsNoTracking() on ii.ExpectedLocationId equals expected.Id
            join foundLocation in _db.Locations.AsNoTracking() on ii.FoundLocationId equals foundLocation.Id into foundLocationGroup
            from found in foundLocationGroup.DefaultIfEmpty()
            join processedByUser in _db.AppUsers.AsNoTracking() on ii.ProcessedByUserId equals processedByUser.Id into processedByGroup
            from processedBy in processedByGroup.DefaultIfEmpty()
            where ii.InventoryId == id
            select new
            {
                Item = ii,
                ExpectedLocationName = expected.Name,
                FoundLocationName = found != null ? found.Name : null,
                ProcessedByName = processedBy != null ? processedBy.UserName : null,
            };

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x =>
                EF.Functions.Like(x.Item.SnapshotInventoryNumber, $"%{text}%") ||
                EF.Functions.Like(x.Item.SnapshotEquipmentName, $"%{text}%"));
        }

        if (query.IsFound is bool isFound)
        {
            filtered = filtered.Where(x => x.Item.IsFound == isFound);
        }

        if (query.IsDamaged is bool isDamaged)
        {
            filtered = filtered.Where(x => x.Item.IsDamaged == isDamaged);
        }

        if (query.FoundLocationId is int foundLocationId)
        {
            filtered = filtered.Where(x => x.Item.FoundLocationId == foundLocationId);
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "equipment" => descending ? filtered.OrderByDescending(x => x.Item.SnapshotEquipmentName) : filtered.OrderBy(x => x.Item.SnapshotEquipmentName),
            "foundlocation" => descending ? filtered.OrderByDescending(x => x.FoundLocationName) : filtered.OrderBy(x => x.FoundLocationName),
            _ => descending ? filtered.OrderByDescending(x => x.Item.SnapshotInventoryNumber) : filtered.OrderBy(x => x.Item.SnapshotInventoryNumber),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InventoryItemDto(
                x.Item.Id, x.Item.InventoryId, x.Item.EquipmentId,
                x.Item.SnapshotInventoryNumber, x.Item.SnapshotEquipmentName, x.Item.SnapshotCategoryName, x.Item.SnapshotStatusName,
                x.Item.ExpectedLocationId, x.ExpectedLocationName,
                x.Item.FoundLocationId, x.FoundLocationName,
                x.Item.IsFound, x.Item.IsDamaged,
                x.Item.ProcessedAtUtc, x.Item.ProcessedByUserId, x.ProcessedByName,
                x.Item.Note))
            .ToListAsync(ct);

        return Ok(new PagedResult<InventoryItemDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}/discrepancies")]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<List<InventoryItemDto>>> GetDiscrepancies(int id, CancellationToken ct)
    {
        var inventory = await _db.Inventories.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        // Same 3-way discrepancy definition as GetInventoryById's DiscrepancyCount: not found, OR
        // found at a different location than expected, OR damaged.
        var items = await (
            from ii in _db.InventoryItems.AsNoTracking()
            join expected in _db.Locations.AsNoTracking() on ii.ExpectedLocationId equals expected.Id
            join foundLocation in _db.Locations.AsNoTracking() on ii.FoundLocationId equals foundLocation.Id into foundLocationGroup
            from found in foundLocationGroup.DefaultIfEmpty()
            join processedByUser in _db.AppUsers.AsNoTracking() on ii.ProcessedByUserId equals processedByUser.Id into processedByGroup
            from processedBy in processedByGroup.DefaultIfEmpty()
            where ii.InventoryId == id &&
                (ii.IsFound == false || ii.IsDamaged == true || (ii.IsFound == true && ii.FoundLocationId != null && ii.FoundLocationId != ii.ExpectedLocationId))
            orderby ii.SnapshotInventoryNumber
            select new InventoryItemDto(
                ii.Id, ii.InventoryId, ii.EquipmentId,
                ii.SnapshotInventoryNumber, ii.SnapshotEquipmentName, ii.SnapshotCategoryName, ii.SnapshotStatusName,
                ii.ExpectedLocationId, expected.Name,
                ii.FoundLocationId, found != null ? found.Name : null,
                ii.IsFound, ii.IsDamaged,
                ii.ProcessedAtUtc, ii.ProcessedByUserId, processedBy != null ? processedBy.UserName : null,
                ii.Note)
        ).ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<InventoryDetailDto>> CreateInventory([FromBody] CreateInventoryDto request, CancellationToken ct)
    {
        var location = await _db.Locations.SingleOrDefaultAsync(l => l.Id == request.LocationId && l.IsActive, ct);
        if (location is null)
        {
            return Problem(detail: "Odabrana lokacija ne postoji ili nije aktivna.", statusCode: StatusCodes.Status400BadRequest);
        }

        int responsibleEmployeeId;
        if (CallerIsManager())
        {
            if (request.ResponsibleEmployeeId is not int candidateId)
            {
                return Problem(detail: "Potrebno je odabrati odgovornu osobu.", statusCode: StatusCodes.Status400BadRequest);
            }

            var candidate = await _db.Employees.SingleOrDefaultAsync(e => e.Id == candidateId, ct);
            if (candidate is null || !candidate.IsActive)
            {
                return Problem(detail: "Odabrana odgovorna osoba ne postoji ili nije aktivna.", statusCode: StatusCodes.Status400BadRequest);
            }

            // Ownership (decision #1) is checked via the ACTING employee's own Employee.LocationId,
            // not ResponsibleEmployeeId - an employee recorded as responsible for a location they
            // don't work at could never actually act as LocationResponsible on their own inventory.
            // Without this check an Admin/Manager could silently create an orphaned-in-practice
            // record.
            if (candidate.LocationId != request.LocationId)
            {
                return Problem(detail: "Odgovorna osoba mora raditi na odabranoj lokaciji.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!await CandidateHasLocationResponsibleRoleAsync(candidateId, ct))
            {
                return Problem(detail: "Odabrana osoba nema ulogu odgovorne osobe lokacije.", statusCode: StatusCodes.Status400BadRequest);
            }

            responsibleEmployeeId = candidateId;
        }
        else
        {
            var callerLocationId = await GetCallerLocationIdAsync(ct);
            if (callerLocationId != request.LocationId)
            {
                return Problem(detail: "Možete kreirati inventuru samo za vlastitu lokaciju.", statusCode: StatusCodes.Status403Forbidden);
            }

            // Force-set regardless of any client-supplied value - real server-side enforcement of
            // planning decision #2, not just a hidden UI field.
            responsibleEmployeeId = _currentUser.EmployeeId!.Value;
        }

        var inventory = new Inventory
        {
            LocationId = request.LocationId,
            ResponsibleEmployeeId = responsibleEmployeeId,
            InventoryStatusId = 1,
            CreatedByUserId = _currentUser.UserId,
            Note = request.Note,
        };

        _db.Inventories.Add(inventory);
        await _db.SaveChangesAsync(ct);

        var responsibleName = await _db.Employees.Where(e => e.Id == responsibleEmployeeId).Select(e => e.FirstName + " " + e.LastName).SingleAsync(ct);
        var statusName = await _db.InventoryStatuses.Where(s => s.Id == inventory.InventoryStatusId).Select(s => s.Name).SingleAsync(ct);

        var dto = new InventoryDetailDto(
            inventory.Id, inventory.LocationId, location.Name,
            inventory.ResponsibleEmployeeId, responsibleName,
            inventory.InventoryStatusId, statusName,
            inventory.OpenedAtUtc, inventory.CompletedAtUtc, inventory.LockedAtUtc,
            inventory.CreatedByUserId, _currentUser.DisplayName, inventory.CreatedAtUtc, inventory.Note,
            TotalItemCount: 0, ProcessedItemCount: 0, FoundCount: 0, MissingCount: 0, DamagedCount: 0, DiscrepancyCount: 0);

        return CreatedAtAction(nameof(GetInventoryById), new { id = inventory.Id }, dto);
    }

    [HttpPost("{id:int}/open")]
    [Authorize(Policy = "LocationWork")]
    public async Task<IActionResult> OpenInventory(int id, CancellationToken ct)
    {
        var inventory = await _db.Inventories.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (inventory.InventoryStatusId != 1)
        {
            return Problem(detail: "Inventura se može otvoriti samo iz statusa Nacrt.", statusCode: StatusCodes.Status409Conflict);
        }

        // Equipment "currently expected" at this location - not archived, current location matches.
        // Deliberately does NOT exclude equipment already captured by another still-open inventory
        // at the same location: no one-inventory-per-location constraint is required by the
        // guidelines, and adding one here would be speculative scope beyond what's asked.
        var equipmentToSnapshot = await (
            from e in _db.Equipment.AsNoTracking()
            join c in _db.EquipmentCategories.AsNoTracking() on e.EquipmentCategoryId equals c.Id
            join s in _db.EquipmentStatuses.AsNoTracking() on e.EquipmentStatusId equals s.Id
            where e.CurrentLocationId == inventory.LocationId && !e.IsArchived
            select new { e.Id, e.InventoryNumber, e.Name, CategoryName = c.Name, StatusName = s.Name }
        ).ToListAsync(ct);

        foreach (var equipment in equipmentToSnapshot)
        {
            _db.InventoryItems.Add(new InventoryItem
            {
                InventoryId = id,
                EquipmentId = equipment.Id,
                ExpectedLocationId = inventory.LocationId,
                SnapshotInventoryNumber = equipment.InventoryNumber,
                SnapshotEquipmentName = equipment.Name,
                SnapshotCategoryName = equipment.CategoryName,
                SnapshotStatusName = equipment.StatusName,
            });
        }

        inventory.InventoryStatusId = 2;
        inventory.OpenedAtUtc = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race backstop - UX_InventoryItems_OnePerEquipment closes a concurrent double-open.
            return Problem(
                detail: "Inventura je u međuvremenu već otvorena ili je došlo do sukoba pri generiranju stavki.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return NoContent();
    }

    [HttpPut("{id:int}/items/{itemId:int}")]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<InventoryItemDto>> UpdateInventoryItem(int id, int itemId, [FromBody] UpdateInventoryItemDto request, CancellationToken ct)
    {
        var inventory = await _db.Inventories.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        var item = await _db.InventoryItems.SingleOrDefaultAsync(ii => ii.Id == itemId && ii.InventoryId == id, ct);
        if (item is null)
        {
            return NotFound();
        }

        // Editing is allowed all the way through Zavrsena (4) - blocked only at Zakljucana (5), per
        // the guidelines' literal "a locked inventory's results can no longer change".
        if (inventory.InventoryStatusId == 5)
        {
            return Problem(detail: "Inventura je zaključana pa se stavke više ne mogu mijenjati.", statusCode: StatusCodes.Status409Conflict);
        }

        if (request.IsFound && request.FoundLocationId is null)
        {
            return Problem(detail: "Potrebno je odabrati lokaciju na kojoj je oprema pronađena.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.FoundLocationId is int requestedFoundLocationId && !await _db.Locations.AnyAsync(l => l.Id == requestedFoundLocationId, ct))
        {
            return Problem(detail: "Odabrana lokacija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        item.IsFound = request.IsFound;
        item.FoundLocationId = request.IsFound ? request.FoundLocationId : null;
        item.IsDamaged = request.IsFound && request.IsDamaged; // damaged is only meaningful when found
        item.Note = request.Note;
        item.ProcessedAtUtc = DateTime.UtcNow;
        item.ProcessedByUserId = _currentUser.UserId;

        if (inventory.InventoryStatusId == 2)
        {
            // Otvorena -> U tijeku, on the first item edit only.
            inventory.InventoryStatusId = 3;
        }

        await _db.SaveChangesAsync(ct);

        var expectedLocationName = await _db.Locations.Where(l => l.Id == item.ExpectedLocationId).Select(l => l.Name).SingleAsync(ct);
        var foundLocationName = item.FoundLocationId is int foundLocationId
            ? await _db.Locations.Where(l => l.Id == foundLocationId).Select(l => l.Name).SingleOrDefaultAsync(ct)
            : null;

        var dto = new InventoryItemDto(
            item.Id, item.InventoryId, item.EquipmentId,
            item.SnapshotInventoryNumber, item.SnapshotEquipmentName, item.SnapshotCategoryName, item.SnapshotStatusName,
            item.ExpectedLocationId, expectedLocationName,
            item.FoundLocationId, foundLocationName,
            item.IsFound, item.IsDamaged,
            item.ProcessedAtUtc, item.ProcessedByUserId, _currentUser.DisplayName,
            item.Note);

        return Ok(dto);
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = "LocationWork")]
    public async Task<IActionResult> CompleteInventory(int id, CancellationToken ct)
    {
        var inventory = await _db.Inventories.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (inventory.InventoryStatusId is not (2 or 3))
        {
            return Problem(detail: "Inventura se može završiti samo iz statusa Otvorena ili U tijeku.", statusCode: StatusCodes.Status409Conflict);
        }

        if (await _db.InventoryItems.AnyAsync(ii => ii.InventoryId == id && ii.IsFound == null, ct))
        {
            return Problem(
                detail: "Sve stavke inventure moraju biti obrađene (pronađeno/nedostaje) prije završetka.",
                statusCode: StatusCodes.Status409Conflict);
        }

        inventory.InventoryStatusId = 4;
        inventory.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // Must come from Zavrsena (4) - consistent with the linear Nacrt -> Otvorena -> U tijeku ->
    // Zavrsena -> Zakljucana flow, not "anything short of Zakljucana".
    [HttpPost("{id:int}/lock")]
    [Authorize(Policy = "LocationWork")]
    public async Task<IActionResult> LockInventory(int id, CancellationToken ct)
    {
        var inventory = await _db.Inventories.SingleOrDefaultAsync(i => i.Id == id, ct);
        if (inventory is null)
        {
            return NotFound();
        }

        if (!await CallerCanAccessLocationAsync(inventory.LocationId, ct))
        {
            return Problem(detail: "Nemate ovlasti za rad s ovom inventurom.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (inventory.InventoryStatusId != 4)
        {
            return Problem(detail: "Inventura se može zaključati samo iz statusa Završena.", statusCode: StatusCodes.Status409Conflict);
        }

        inventory.InventoryStatusId = 5;
        inventory.LockedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // Small, single-purpose addition needed so CreateInventoryDialog can show/hide the Location
    // field correctly for a LocationResponsible caller - ICurrentUser carries no LocationId claim
    // client-side. Harmless for Admin/Manager callers (returns their own linked employee's
    // location, or null) - the dialog only uses it for the non-manager branch.
    [HttpGet("my-location")]
    [Authorize(Policy = "LocationWork")]
    public async Task<ActionResult<LookupDto?>> GetMyLocation(CancellationToken ct)
    {
        if (_currentUser.EmployeeId is not int employeeId)
        {
            return Ok(null);
        }

        var location = await (
            from e in _db.Employees.AsNoTracking()
            join l in _db.Locations.AsNoTracking() on e.LocationId equals l.Id
            where e.Id == employeeId
            select new LookupDto(l.Id, l.Name)
        ).SingleOrDefaultAsync(ct);

        return Ok(location);
    }
}
