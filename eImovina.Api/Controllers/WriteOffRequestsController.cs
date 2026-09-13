using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Api.Services;
using eImovina.Shared.Common;
using eImovina.Shared.WriteOffRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/writeoffrequests")]
public class WriteOffRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public WriteOffRequestsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private async Task<WriteOffRequestDetailDto?> GetDetailAsync(int id, CancellationToken ct) =>
        await (
            from w in _db.WriteOffRequests.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on w.EquipmentId equals e.Id
            join s in _db.WriteOffRequestStatuses.AsNoTracking() on w.WriteOffRequestStatusId equals s.Id
            join submittedBy in _db.AppUsers.AsNoTracking() on w.SubmittedByUserId equals submittedBy.Id
            join decidedByUser in _db.AppUsers.AsNoTracking() on w.DecisionByUserId equals decidedByUser.Id into decidedByGroup
            from decidedBy in decidedByGroup.DefaultIfEmpty()
            join executedByUser in _db.AppUsers.AsNoTracking() on w.ExecutedByUserId equals executedByUser.Id into executedByGroup
            from executedBy in executedByGroup.DefaultIfEmpty()
            where w.Id == id
            select new WriteOffRequestDetailDto(
                w.Id, w.EquipmentId, e.InventoryNumber, e.Name,
                w.WriteOffRequestStatusId, s.Name,
                w.Reason, w.SubmittedByUserId, submittedBy.UserName, w.CreatedAtUtc,
                w.DecisionByUserId, decidedBy != null ? decidedBy.UserName : null, w.DecisionAtUtc, w.DecisionNote,
                w.ExecutedAtUtc, w.ExecutedByUserId, executedBy != null ? executedBy.UserName : null)
        ).SingleOrDefaultAsync(ct);

    [HttpGet]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<PagedResult<WriteOffRequestListDto>>> GetWriteOffRequests([FromQuery] WriteOffRequestQuery query, CancellationToken ct)
    {
        var filtered =
            from w in _db.WriteOffRequests.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on w.EquipmentId equals e.Id
            join s in _db.WriteOffRequestStatuses.AsNoTracking() on w.WriteOffRequestStatusId equals s.Id
            join submittedBy in _db.AppUsers.AsNoTracking() on w.SubmittedByUserId equals submittedBy.Id
            select new
            {
                WriteOff = w,
                EquipmentInventoryNumber = e.InventoryNumber,
                EquipmentName = e.Name,
                StatusName = s.Name,
                SubmittedByName = submittedBy.UserName,
            };

        if (query.EquipmentId is int equipmentId)
        {
            filtered = filtered.Where(x => x.WriteOff.EquipmentId == equipmentId);
        }

        if (query.WriteOffRequestStatusId is int statusId)
        {
            filtered = filtered.Where(x => x.WriteOff.WriteOffRequestStatusId == statusId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x => EF.Functions.Like(x.WriteOff.Reason, $"%{text}%"));
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "equipment" => descending ? filtered.OrderByDescending(x => x.EquipmentInventoryNumber) : filtered.OrderBy(x => x.EquipmentInventoryNumber),
            "reason" => descending ? filtered.OrderByDescending(x => x.WriteOff.Reason) : filtered.OrderBy(x => x.WriteOff.Reason),
            "status" => descending ? filtered.OrderByDescending(x => x.StatusName) : filtered.OrderBy(x => x.StatusName),
            "submittedby" => descending ? filtered.OrderByDescending(x => x.SubmittedByName) : filtered.OrderBy(x => x.SubmittedByName),
            "createdat" => descending ? filtered.OrderByDescending(x => x.WriteOff.CreatedAtUtc) : filtered.OrderBy(x => x.WriteOff.CreatedAtUtc),
            _ => filtered.OrderByDescending(x => x.WriteOff.CreatedAtUtc),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WriteOffRequestListDto(
                x.WriteOff.Id, x.WriteOff.EquipmentId, x.EquipmentInventoryNumber, x.EquipmentName,
                x.WriteOff.WriteOffRequestStatusId, x.StatusName,
                x.WriteOff.Reason, x.WriteOff.SubmittedByUserId, x.SubmittedByName,
                x.WriteOff.CreatedAtUtc, x.WriteOff.DecisionAtUtc, x.WriteOff.ExecutedAtUtc))
            .ToListAsync(ct);

        return Ok(new PagedResult<WriteOffRequestListDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<WriteOffRequestDetailDto>> GetById(int id, CancellationToken ct)
    {
        var dto = await GetDetailAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<WriteOffRequestDetailDto>> Create([FromBody] CreateWriteOffRequestDto request, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([request.EquipmentId], ct);
        if (equipment is null)
        {
            return Problem(detail: "Odabrana oprema ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (equipment.EquipmentStatusId == 5)
        {
            return Problem(detail: "Oprema je već otpisana.", statusCode: StatusCodes.Status409Conflict);
        }

        if (await _db.WriteOffRequests.AnyAsync(w => w.EquipmentId == request.EquipmentId && w.WriteOffRequestStatusId != 4 && w.WriteOffRequestStatusId != 5, ct))
        {
            return Problem(detail: "Oprema već ima otvoren zahtjev za otpis.", statusCode: StatusCodes.Status409Conflict);
        }

        var writeOff = new WriteOffRequest
        {
            EquipmentId = request.EquipmentId,
            SubmittedByUserId = _currentUser.UserId,
            WriteOffRequestStatusId = 1,
            Reason = request.Reason,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.WriteOffRequests.Add(writeOff);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // UX_WriteOff_OneOpenPerEquipment is the real backstop against a race between two
            // concurrent create calls for the same equipment - mirrors AssignEquipment's pattern.
            return Problem(detail: "Oprema je u međuvremenu već dobila otvoren zahtjev za otpis.", statusCode: StatusCodes.Status409Conflict);
        }

        var dto = await GetDetailAsync(writeOff.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = writeOff.Id }, dto);
    }

    [HttpPost("{id:int}/decide")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<WriteOffRequestDetailDto>> Decide(int id, [FromBody] DecideWriteOffRequestDto request, CancellationToken ct)
    {
        var writeOff = await _db.WriteOffRequests.FindAsync([id], ct);
        if (writeOff is null)
        {
            return NotFound();
        }

        if (writeOff.WriteOffRequestStatusId is not (1 or 2))
        {
            return Problem(detail: "Zahtjev je već odlučen.", statusCode: StatusCodes.Status409Conflict);
        }

        writeOff.WriteOffRequestStatusId = request.WriteOffRequestStatusId;
        writeOff.DecisionByUserId = _currentUser.UserId;
        writeOff.DecisionAtUtc = DateTime.UtcNow;
        writeOff.DecisionNote = request.DecisionNote;

        await _db.SaveChangesAsync(ct);

        return Ok(await GetDetailAsync(id, ct));
    }

    [HttpPost("{id:int}/execute")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<WriteOffRequestDetailDto>> Execute(int id, CancellationToken ct)
    {
        var writeOff = await _db.WriteOffRequests.FindAsync([id], ct);
        if (writeOff is null)
        {
            return NotFound();
        }

        if (writeOff.WriteOffRequestStatusId != 3)
        {
            return Problem(detail: "Otpis se može provesti samo za odobrene zahtjeve.", statusCode: StatusCodes.Status409Conflict);
        }

        var equipment = await _db.Equipment.FindAsync([writeOff.EquipmentId], ct);
        if (equipment is null)
        {
            return Problem(detail: "Oprema više ne postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        var activeAssignment = await _db.EquipmentAssignments
            .SingleOrDefaultAsync(a => a.EquipmentId == writeOff.EquipmentId && a.AssignmentStatusId == 1, ct);

        var now = DateTime.UtcNow;
        if (activeAssignment is not null)
        {
            activeAssignment.AssignmentStatusId = 2;
            activeAssignment.ReturnedAtUtc = now;
            activeAssignment.ClosedByUserId = _currentUser.UserId;
        }

        EquipmentHistoryRecorder.RecordStatusChange(_db, equipment.Id, equipment.EquipmentStatusId, 5, _currentUser.UserId, now);
        equipment.EquipmentStatusId = 5;
        writeOff.WriteOffRequestStatusId = 5;
        writeOff.ExecutedAtUtc = now;
        writeOff.ExecutedByUserId = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Ok(await GetDetailAsync(id, ct));
    }
}
