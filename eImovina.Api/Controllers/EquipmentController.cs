using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Api.Services;
using eImovina.Shared.Common;
using eImovina.Shared.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

/// <summary>
/// [FromForm]-bound multipart request for POST {id}/files - can't live in eImovina.Shared since
/// IFormFile is an ASP.NET Core type, not a plain DTO type.
/// </summary>
public sealed class UploadEquipmentFileRequest
{
    public IFormFile File { get; set; } = null!;
    public FileKind FileKind { get; set; }
}

[ApiController]
[Route("api/equipment")]
public class EquipmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ICurrentUser _currentUser;

    public EquipmentController(AppDbContext db, IWebHostEnvironment env, ICurrentUser currentUser)
    {
        _db = db;
        _env = env;
        _currentUser = currentUser;
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
            // Defaults to active-only (matches every existing caller's expectation) - pass
            // ?IsArchived=true explicitly to see archived equipment instead. A tri-state "both" was
            // not requested and isn't worth the added complexity here.
            where e.IsArchived == (query.IsArchived ?? false)
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
            .Select(x => new EquipmentListItemDto(
                x.Equipment.Id, x.Equipment.InventoryNumber, x.Equipment.SerialNumber, x.Equipment.Name, x.CategoryName, x.StatusName, x.LocationName, x.Equipment.PurchaseValue, x.Equipment.Currency,
                _db.EquipmentFiles.Where(f => f.EquipmentId == x.Equipment.Id && f.IsCoverImage).Select(f => (int?)f.Id).FirstOrDefault(),
                x.Equipment.IsArchived))
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
                e.PurchaseValue, e.Currency, e.PurchaseDate, e.Notes, e.IsArchived, e.CreatedAtUtc,
                (from a in _db.EquipmentAssignments
                 join emp in _db.Employees on a.EmployeeId equals emp.Id
                 where a.EquipmentId == e.Id && a.AssignmentStatusId == 1
                 select emp.FirstName + " " + emp.LastName).FirstOrDefault(),
                _db.EquipmentAssignments.Count(a => a.EquipmentId == e.Id),
                _db.EquipmentFiles.Count(f => f.EquipmentId == e.Id),
                _db.WriteOffRequests.Count(w => w.EquipmentId == e.Id))
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
            equipment.PurchaseValue, equipment.Currency, equipment.PurchaseDate, equipment.Notes, equipment.IsArchived, equipment.CreatedAtUtc,
            CurrentAssigneeName: null, AssignmentHistoryCount: 0, FileCount: 0, WriteOffRequestCount: 0);

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

        // EquipmentStatusId == 2 (Zaduženo) must always correspond to exactly one active
        // assignment row - enforced symmetrically here so the general edit form can't desync the
        // two. The real assign/return/transfer workflow (AssignmentsController) is the only way to
        // create or close that correspondence.
        var hasActiveAssignment = await HasActiveAssignmentAsync(id, ct);
        if (hasActiveAssignment && request.EquipmentStatusId != 2)
        {
            return Problem(
                detail: "Oprema je trenutačno zadužena — status se ne može mijenjati dok se prvo ne izvrši povrat ili premještaj.",
                statusCode: StatusCodes.Status409Conflict);
        }
        if (!hasActiveAssignment && request.EquipmentStatusId == 2)
        {
            return Problem(
                detail: "Status 'Zaduženo' se ne može postaviti ručno — potrebno je zadužiti opremu kroz akciju zaduživanja.",
                statusCode: StatusCodes.Status409Conflict);
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

    // Task-required dedicated action alongside the general PUT (which already lets a manager
    // change CurrentLocationId too, from Section 8) - both are kept; this one never touches
    // EquipmentAssignments at all, which is what "without necessarily ending the assignment" means
    // here: there's structurally nothing here that could accidentally close one. Allowed
    // unconditionally, including while the equipment is actively assigned (Zaduženo) - confirmed
    // with the user during Section 10 planning, and directly supported by the guideline's own
    // wording that a location change doesn't necessarily end the assignment.
    [HttpPost("{id:int}/change-location")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> ChangeLocation(int id, [FromBody] ChangeLocationDto request, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([id], ct);
        if (equipment is null)
        {
            return NotFound();
        }

        if (!await _db.Locations.AnyAsync(l => l.Id == request.NewLocationId, ct))
        {
            return Problem(detail: "Odabrana lokacija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        equipment.CurrentLocationId = request.NewLocationId;
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

    // Symmetric undo for ArchiveEquipment - no extra validation needed since reactivating changes
    // nothing but the archive flag itself; every other field was already valid when the row was
    // created/last edited.
    [HttpPost("{id:int}/reactivate")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<ReactivateEquipmentResultDto>> ReactivateEquipment(int id, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([id], ct);
        if (equipment is null)
        {
            return NotFound();
        }

        if (!equipment.IsArchived)
        {
            return Problem(detail: "Oprema nije arhivirana.", statusCode: StatusCodes.Status409Conflict);
        }

        equipment.IsArchived = false;
        equipment.ArchivedAtUtc = null;
        await _db.SaveChangesAsync(ct);

        // Inventory item snapshots are frozen at open time (PROJECT_GUIDELINES.md) - reactivated
        // equipment does NOT retroactively join a currently-open inventory at its location. Report
        // whether one exists so the UI can tell the user why it won't show up until the next one.
        var hasLiveInventoryAtLocation = await _db.Inventories.AnyAsync(
            i => i.LocationId == equipment.CurrentLocationId && (i.InventoryStatusId == 2 || i.InventoryStatusId == 3), ct);

        return Ok(new ReactivateEquipmentResultDto(hasLiveInventoryAtLocation));
    }

    private async Task<bool> IsEquipmentReferencedAsync(int id, CancellationToken ct)
    {
        var pendingWriteOffStatusIds = new[] { 1, 2, 3 }; // Zaprimljeno, U obradi, Odobreno (not Odbijeno/Provedeno)
        // Only a still-live inventory (not yet Zakljucana) blocks archiving - a locked, historical
        // inventory's captured snapshot is immutable regardless of the equipment's later archive
        // state, so it shouldn't hold the equipment hostage forever (this was previously checked
        // unconditionally against all-time history, contradicting the comment above - same class of
        // bug as the one already fixed in LocationsController.IsLocationReferencedAsync).
        var liveInventoryStatusIds = new[] { 2, 3, 4 }; // Otvorena, U tijeku, Zavrsena
        return await HasActiveAssignmentAsync(id, ct)
            || await (from ii in _db.InventoryItems
                      join inv in _db.Inventories on ii.InventoryId equals inv.Id
                      where ii.EquipmentId == id && liveInventoryStatusIds.Contains(inv.InventoryStatusId)
                      select ii).AnyAsync(ct)
            || await _db.WriteOffRequests.AnyAsync(w => w.EquipmentId == id && pendingWriteOffStatusIds.Contains(w.WriteOffRequestStatusId), ct);
    }

    private Task<bool> HasActiveAssignmentAsync(int id, CancellationToken ct) =>
        _db.EquipmentAssignments.AnyAsync(a => a.EquipmentId == id && a.AssignmentStatusId == 1, ct);

    // Read access is intentionally broader than the rest of this controller (plain [Authorize]
    // instead of the InventoryManagement policy used everywhere else here): an employee who isn't
    // an inventory manager is meant to be able to view files on equipment assigned to them
    // (Section 10's /mine scope) without needing the management role. Since [Authorize] attributes
    // on this controller are per-action (not class-level), a plain [Authorize] here genuinely
    // means "any authenticated user" - it has no stricter class-level attribute to AND against.
    [HttpGet("{id:int}/files")]
    [Authorize]
    public async Task<ActionResult<List<EquipmentFileDto>>> GetEquipmentFiles(int id, CancellationToken ct)
    {
        if (!await _db.Equipment.AnyAsync(e => e.Id == id, ct))
        {
            return NotFound();
        }

        var files = await _db.EquipmentFiles.AsNoTracking()
            .Where(f => f.EquipmentId == id)
            .OrderByDescending(f => f.IsCoverImage)
            .ThenBy(f => f.UploadedAtUtc)
            .ToListAsync(ct);

        var dtos = files
            .Select(f => new EquipmentFileDto(f.Id, f.EquipmentId, MapToSharedFileKind(f.FileKind), f.OriginalFileName, f.ContentType, f.SizeBytes, f.UploadedAtUtc, f.IsCoverImage))
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("{id:int}/files")]
    [Authorize(Policy = "InventoryManagement")]
    [RequestSizeLimit(UploadLimits.MaxFileSizeBytes + 4096)]
    public async Task<ActionResult<EquipmentFileDto>> UploadEquipmentFile(int id, [FromForm] UploadEquipmentFileRequest request, CancellationToken ct)
    {
        if (!await _db.Equipment.AnyAsync(e => e.Id == id, ct))
        {
            return NotFound();
        }

        if (request.File is null || request.File.Length == 0)
        {
            return Problem(detail: "Datoteka je obavezna.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.File.Length > UploadLimits.MaxFileSizeBytes)
        {
            return Problem(
                detail: $"Datoteka je prevelika (najviše {UploadLimits.MaxFileSizeBytes / (1024 * 1024)} MB).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Extension allow-list is authoritative, not the client-supplied IFormFile.ContentType
        // header (spoofable) - the ContentType actually stored/served is the canonical value from
        // this same mapping, never the raw header, so GET /api/files/{id} never round-trips
        // garbage back out as this file's Content-Type.
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        var allowedExtensions = request.FileKind == FileKind.Image ? UploadLimits.AllowedImageExtensions : UploadLimits.AllowedDocumentExtensions;
        if (!allowedExtensions.TryGetValue(extension, out var canonicalContentType))
        {
            return Problem(
                detail: request.FileKind == FileKind.Image
                    ? "Za sliku su dopušteni formati: PNG, JPG, WEBP."
                    : "Za dokument je dopušten samo PDF format.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Flat storage, no per-equipment subfolders - StoredFileName and RelativePath are always
        // the same value; see UploadPaths for the one place that convention is spelled out.
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var uploadsRoot = UploadPaths.Root(_env);
        Directory.CreateDirectory(uploadsRoot);
        var fullPath = UploadPaths.FullPath(_env, storedFileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await request.File.CopyToAsync(stream, ct);
        }

        var entity = new EquipmentFile
        {
            EquipmentId = id,
            FileKind = MapToEntityFileKind(request.FileKind),
            OriginalFileName = request.File.FileName,
            StoredFileName = storedFileName,
            RelativePath = storedFileName,
            ContentType = canonicalContentType,
            SizeBytes = request.File.Length,
            UploadedAtUtc = DateTime.UtcNow,
            UploadedByUserId = _currentUser.UserId,
            IsCoverImage = false,
        };

        _db.EquipmentFiles.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = new EquipmentFileDto(entity.Id, entity.EquipmentId, request.FileKind, entity.OriginalFileName, entity.ContentType, entity.SizeBytes, entity.UploadedAtUtc, entity.IsCoverImage);
        return CreatedAtAction(nameof(GetEquipmentFiles), new { id }, dto);
    }

    [HttpPost("{id:int}/files/{fileId:int}/set-cover")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> SetCoverImage(int id, int fileId, CancellationToken ct)
    {
        var file = await _db.EquipmentFiles.SingleOrDefaultAsync(f => f.Id == fileId && f.EquipmentId == id, ct);
        if (file is null)
        {
            return NotFound();
        }

        if (file.FileKind != EquipmentFileKind.Image)
        {
            return Problem(detail: "Samo slika može biti postavljena kao naslovna.", statusCode: StatusCodes.Status400BadRequest);
        }

        var otherCovers = await _db.EquipmentFiles
            .Where(f => f.EquipmentId == id && f.IsCoverImage && f.Id != fileId)
            .ToListAsync(ct);
        foreach (var cover in otherCovers)
        {
            cover.IsCoverImage = false;
        }

        file.IsCoverImage = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // FileKind (Shared, DTO-facing) <-> EquipmentFileKind (entity-facing) are mapped by name, not
    // by casting the underlying int - the two enums have identical members today, but a raw cast
    // would silently corrupt data with no compiler warning if either one is ever reordered.
    private static FileKind MapToSharedFileKind(EquipmentFileKind kind) => kind switch
    {
        EquipmentFileKind.Image => FileKind.Image,
        EquipmentFileKind.Invoice => FileKind.Invoice,
        EquipmentFileKind.Warranty => FileKind.Warranty,
        EquipmentFileKind.ServiceDoc => FileKind.ServiceDoc,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static EquipmentFileKind MapToEntityFileKind(FileKind kind) => kind switch
    {
        FileKind.Image => EquipmentFileKind.Image,
        FileKind.Invoice => EquipmentFileKind.Invoice,
        FileKind.Warranty => EquipmentFileKind.Warranty,
        FileKind.ServiceDoc => EquipmentFileKind.ServiceDoc,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
