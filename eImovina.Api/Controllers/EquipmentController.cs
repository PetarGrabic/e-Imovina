using eImovina.Api.Data;
using eImovina.Shared.Common;
using eImovina.Shared.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/equipment")]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;

    public EquipmentController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<PagedResult<EquipmentListItemDto>>> GetEquipment([FromQuery] EquipmentQuery query, CancellationToken ct)
    {
        var filtered =
            from e in _db.Equipment.AsNoTracking()
            join c in _db.EquipmentCategories.AsNoTracking() on e.EquipmentCategoryId equals c.Id
            join s in _db.EquipmentStatuses.AsNoTracking() on e.EquipmentStatusId equals s.Id
            join l in _db.Locations.AsNoTracking() on e.CurrentLocationId equals l.Id
            where !e.IsArchived
            select new { Equipment = e, CategoryName = c.Name, StatusName = s.Name, LocationName = l.Name };

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x =>
                EF.Functions.Like(x.Equipment.Name, $"%{text}%") ||
                EF.Functions.Like(x.Equipment.InventoryNumber, $"%{text}%") ||
                (x.Equipment.SerialNumber != null && EF.Functions.Like(x.Equipment.SerialNumber, $"%{text}%")));
        }

        if (query.CategoryId is int categoryId)
        {
            filtered = filtered.Where(x => x.Equipment.EquipmentCategoryId == categoryId);
        }

        if (query.StatusId is int statusId)
        {
            filtered = filtered.Where(x => x.Equipment.EquipmentStatusId == statusId);
        }

        if (query.LocationId is int locationId)
        {
            filtered = filtered.Where(x => x.Equipment.CurrentLocationId == locationId);
        }

        if (query.EmployeeId is int employeeId)
        {
            filtered = filtered.Where(x => _db.EquipmentAssignments.Any(a =>
                a.EquipmentId == x.Equipment.Id && a.AssignmentStatusId == 1 && a.EmployeeId == employeeId));
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "inventorynumber" => descending ? filtered.OrderByDescending(x => x.Equipment.InventoryNumber) : filtered.OrderBy(x => x.Equipment.InventoryNumber),
            "category" => descending ? filtered.OrderByDescending(x => x.CategoryName) : filtered.OrderBy(x => x.CategoryName),
            "status" => descending ? filtered.OrderByDescending(x => x.StatusName) : filtered.OrderBy(x => x.StatusName),
            "location" => descending ? filtered.OrderByDescending(x => x.LocationName) : filtered.OrderBy(x => x.LocationName),
            _ => descending ? filtered.OrderByDescending(x => x.Equipment.Name) : filtered.OrderBy(x => x.Equipment.Name),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EquipmentListItemDto(x.Equipment.Id, x.Equipment.InventoryNumber, x.Equipment.SerialNumber, x.Equipment.Name, x.CategoryName, x.StatusName, x.LocationName, x.Equipment.PurchaseValue, x.Equipment.Currency))
            .ToListAsync(ct);

        return Ok(new PagedResult<EquipmentListItemDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<EquipmentDetailDto>> GetEquipmentById(int id, CancellationToken ct)
    {
        var dto = await (
            from e in _db.Equipment.AsNoTracking()
            join c in _db.EquipmentCategories.AsNoTracking() on e.EquipmentCategoryId equals c.Id
            join s in _db.EquipmentStatuses.AsNoTracking() on e.EquipmentStatusId equals s.Id
            join l in _db.Locations.AsNoTracking() on e.CurrentLocationId equals l.Id
            where e.Id == id
            select new EquipmentDetailDto(
                e.Id, e.InventoryNumber, e.SerialNumber, e.Name,
                e.EquipmentCategoryId, c.Name, e.EquipmentStatusId, s.Name, e.CurrentLocationId, l.Name,
                e.PurchaseValue, e.Currency, e.PurchaseDate, e.Notes, e.IsArchived, e.CreatedAtUtc)
        ).SingleOrDefaultAsync(ct);

        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<EquipmentDetailDto>> CreateEquipment([FromBody] EquipmentCreateDto request, CancellationToken ct)
    {
        if (await _db.Equipment.AnyAsync(e => e.InventoryNumber == request.InventoryNumber, ct))
        {
            return Problem(detail: $"Oprema s inventurnim brojem '{request.InventoryNumber}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!string.IsNullOrWhiteSpace(request.SerialNumber) && await _db.Equipment.AnyAsync(e => e.SerialNumber == request.SerialNumber, ct))
        {
            return Problem(detail: $"Oprema sa serijskim brojem '{request.SerialNumber}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!await _db.EquipmentCategories.AnyAsync(c => c.Id == request.EquipmentCategoryId, ct))
        {
            return Problem(detail: "Odabrana kategorija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await _db.EquipmentStatuses.AnyAsync(s => s.Id == request.EquipmentStatusId, ct))
        {
            return Problem(detail: "Odabrani status ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await _db.Locations.AnyAsync(l => l.Id == request.CurrentLocationId, ct))
        {
            return Problem(detail: "Odabrana lokacija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        var equipment = new Api.Data.Entities.Equipment
        {
            InventoryNumber = request.InventoryNumber,
            SerialNumber = request.SerialNumber,
            Name = request.Name,
            EquipmentCategoryId = request.EquipmentCategoryId,
            EquipmentStatusId = request.EquipmentStatusId,
            CurrentLocationId = request.CurrentLocationId,
            PurchaseValue = request.PurchaseValue,
            Currency = request.Currency,
            PurchaseDate = request.PurchaseDate,
            Notes = request.Notes,
        };

        _db.Equipment.Add(equipment);
        await _db.SaveChangesAsync(ct);

        var categoryName = await _db.EquipmentCategories.Where(c => c.Id == equipment.EquipmentCategoryId).Select(c => c.Name).SingleAsync(ct);
        var statusName = await _db.EquipmentStatuses.Where(s => s.Id == equipment.EquipmentStatusId).Select(s => s.Name).SingleAsync(ct);
        var locationName = await _db.Locations.Where(l => l.Id == equipment.CurrentLocationId).Select(l => l.Name).SingleAsync(ct);

        var dto = new EquipmentDetailDto(
            equipment.Id, equipment.InventoryNumber, equipment.SerialNumber, equipment.Name,
            equipment.EquipmentCategoryId, categoryName, equipment.EquipmentStatusId, statusName, equipment.CurrentLocationId, locationName,
            equipment.PurchaseValue, equipment.Currency, equipment.PurchaseDate, equipment.Notes, equipment.IsArchived, equipment.CreatedAtUtc);

        return CreatedAtAction(nameof(GetEquipmentById), new { id = equipment.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> UpdateEquipment(int id, [FromBody] EquipmentUpdateDto request, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([id], ct);
        if (equipment is null)
        {
            return NotFound();
        }

        if (await _db.Equipment.AnyAsync(e => e.Id != id && e.InventoryNumber == request.InventoryNumber, ct))
        {
            return Problem(detail: $"Oprema s inventurnim brojem '{request.InventoryNumber}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!string.IsNullOrWhiteSpace(request.SerialNumber) && await _db.Equipment.AnyAsync(e => e.Id != id && e.SerialNumber == request.SerialNumber, ct))
        {
            return Problem(detail: $"Oprema sa serijskim brojem '{request.SerialNumber}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (!await _db.EquipmentCategories.AnyAsync(c => c.Id == request.EquipmentCategoryId, ct))
        {
            return Problem(detail: "Odabrana kategorija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await _db.EquipmentStatuses.AnyAsync(s => s.Id == request.EquipmentStatusId, ct))
        {
            return Problem(detail: "Odabrani status ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!await _db.Locations.AnyAsync(l => l.Id == request.CurrentLocationId, ct))
        {
            return Problem(detail: "Odabrana lokacija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        equipment.InventoryNumber = request.InventoryNumber;
        equipment.SerialNumber = request.SerialNumber;
        equipment.Name = request.Name;
        equipment.EquipmentCategoryId = request.EquipmentCategoryId;
        equipment.EquipmentStatusId = request.EquipmentStatusId;
        equipment.CurrentLocationId = request.CurrentLocationId;
        equipment.PurchaseValue = request.PurchaseValue;
        equipment.Currency = request.Currency;
        equipment.PurchaseDate = request.PurchaseDate;
        equipment.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Equipment is soft-archived, never physically deleted - IsArchived is a separate concept from
    // the write-off workflow's EquipmentStatusId (Section 2 decision). Blocked only by CURRENT
    // conditions (an active assignment, any inventory item, or a still-pending write-off) - not
    // all-time history, which would make archiving permanently impossible for anything ever
    // assigned even once, mirroring the same fix already applied to Locations' deactivate rule.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> ArchiveEquipment(int id, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([id], ct);
        if (equipment is null)
        {
            return NotFound();
        }

        if (await IsEquipmentReferencedAsync(id, ct))
        {
            return Problem(
                detail: "Oprema se ne može arhivirati jer je trenutačno zadužena, dio je inventure ili ima zahtjev za otpis u obradi.",
                statusCode: StatusCodes.Status409Conflict);
        }

        equipment.IsArchived = true;
        equipment.ArchivedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> IsEquipmentReferencedAsync(int id, CancellationToken ct)
    {
        var pendingWriteOffStatusIds = new[] { 1, 2, 3 }; // Zaprimljeno, U obradi, Odobreno (not Odbijeno/Provedeno)
        return await _db.EquipmentAssignments.AnyAsync(a => a.EquipmentId == id && a.AssignmentStatusId == 1, ct)
            || await _db.InventoryItems.AnyAsync(ii => ii.EquipmentId == id, ct)
            || await _db.WriteOffRequests.AnyAsync(w => w.EquipmentId == id && pendingWriteOffStatusIds.Contains(w.WriteOffRequestStatusId), ct);
    }
}
