using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Api.Services;
using eImovina.Shared.Assignments;
using eImovina.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AssignmentsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<PagedResult<AssignmentDto>>> GetAssignments([FromQuery] AssignmentQuery query, CancellationToken ct)
    {
        var filtered =
            from a in _db.EquipmentAssignments.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on a.EquipmentId equals e.Id
            join emp in _db.Employees.AsNoTracking() on a.EmployeeId equals emp.Id
            join s in _db.AssignmentStatuses.AsNoTracking() on a.AssignmentStatusId equals s.Id
            join assignedBy in _db.AppUsers.AsNoTracking() on a.AssignedByUserId equals assignedBy.Id
            join closedByUser in _db.AppUsers.AsNoTracking() on a.ClosedByUserId equals closedByUser.Id into closedByGroup
            from closedBy in closedByGroup.DefaultIfEmpty()
            // Archived equipment is excluded from this general cross-equipment browsing list -
            // Equipment.razor already excludes archived items from its own list, and this general
            // list shouldn't surface them either. GetHistory (below) deliberately does NOT apply
            // this filter, since it's scoped to one already-known equipment id (used by an
            // archived item's own profile page, which should still show its full history).
            where !e.IsArchived
            select new
            {
                Assignment = a,
                Equipment = e,
                Employee = emp,
                StatusName = s.Name,
                AssignedByName = assignedBy.UserName,
                ClosedByName = closedBy != null ? closedBy.UserName : null,
            };

        if (query.EquipmentId is int equipmentId)
        {
            filtered = filtered.Where(x => x.Assignment.EquipmentId == equipmentId);
        }

        if (query.EmployeeId is int employeeId)
        {
            filtered = filtered.Where(x => x.Assignment.EmployeeId == employeeId);
        }

        if (query.StatusId is int statusId)
        {
            filtered = filtered.Where(x => x.Assignment.AssignmentStatusId == statusId);
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "equipment" => descending ? filtered.OrderByDescending(x => x.Equipment.InventoryNumber) : filtered.OrderBy(x => x.Equipment.InventoryNumber),
            "employee" => descending ? filtered.OrderByDescending(x => x.Employee.LastName) : filtered.OrderBy(x => x.Employee.LastName),
            "status" => descending ? filtered.OrderByDescending(x => x.StatusName) : filtered.OrderBy(x => x.StatusName),
            "assignedat" => descending ? filtered.OrderByDescending(x => x.Assignment.AssignedAtUtc) : filtered.OrderBy(x => x.Assignment.AssignedAtUtc),
            "returnedat" => descending ? filtered.OrderByDescending(x => x.Assignment.ReturnedAtUtc) : filtered.OrderBy(x => x.Assignment.ReturnedAtUtc),
            "assignedby" => descending ? filtered.OrderByDescending(x => x.AssignedByName) : filtered.OrderBy(x => x.AssignedByName),
            // No explicit sort requested: most-recent-first is the natural default for a history
            // feed, regardless of Dir (which the frontend didn't set for this case either).
            _ => filtered.OrderByDescending(x => x.Assignment.AssignedAtUtc),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AssignmentDto(
                x.Assignment.Id, x.Equipment.Id, x.Equipment.InventoryNumber, x.Equipment.Name,
                x.Employee.Id, x.Employee.FirstName + " " + x.Employee.LastName,
                x.Assignment.AssignmentStatusId, x.StatusName,
                x.Assignment.AssignedAtUtc, x.Assignment.ReturnedAtUtc,
                x.AssignedByName, x.ClosedByName, x.Assignment.Note))
            .ToListAsync(ct);

        return Ok(new PagedResult<AssignmentDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("history")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<List<AssignmentDto>>> GetHistory([FromQuery] int equipmentId, CancellationToken ct)
    {
        if (!await _db.Equipment.AnyAsync(e => e.Id == equipmentId, ct))
        {
            return NotFound();
        }

        var items = await (
            from a in _db.EquipmentAssignments.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on a.EquipmentId equals e.Id
            join emp in _db.Employees.AsNoTracking() on a.EmployeeId equals emp.Id
            join s in _db.AssignmentStatuses.AsNoTracking() on a.AssignmentStatusId equals s.Id
            join assignedBy in _db.AppUsers.AsNoTracking() on a.AssignedByUserId equals assignedBy.Id
            join closedByUser in _db.AppUsers.AsNoTracking() on a.ClosedByUserId equals closedByUser.Id into closedByGroup
            from closedBy in closedByGroup.DefaultIfEmpty()
            where a.EquipmentId == equipmentId
            orderby a.AssignedAtUtc descending
            select new AssignmentDto(
                a.Id, e.Id, e.InventoryNumber, e.Name,
                emp.Id, emp.FirstName + " " + emp.LastName,
                a.AssignmentStatusId, s.Name,
                a.AssignedAtUtc, a.ReturnedAtUtc,
                assignedBy.UserName, closedBy != null ? closedBy.UserName : null, a.Note)
        ).ToListAsync(ct);

        return Ok(items);
    }

    // Identity comes ONLY from ICurrentUser.EmployeeId (a JWT claim) - never a query/body id.
    // An account with no linked employee (e.g. Admin/InventoryManager) isn't an error case, it just
    // has nothing to show - an empty list needs no special-casing. Plain [Authorize] (any
    // authenticated role), same broadening rationale as Section 9's file-read endpoints.
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<PagedResult<MyAssignmentDto>>> GetMine([FromQuery] MyAssignmentQuery query, CancellationToken ct)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Ok(new PagedResult<MyAssignmentDto>(Array.Empty<MyAssignmentDto>(), 0, page, pageSize));
        }

        var filtered =
            from a in _db.EquipmentAssignments.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on a.EquipmentId equals e.Id
            join s in _db.AssignmentStatuses.AsNoTracking() on a.AssignmentStatusId equals s.Id
            where a.EmployeeId == employeeId && a.AssignmentStatusId == 1
            select new { Assignment = a, Equipment = e, StatusName = s.Name };

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x =>
                EF.Functions.Like(x.Equipment.Name, $"%{text}%") ||
                EF.Functions.Like(x.Equipment.InventoryNumber, $"%{text}%"));
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "inventorynumber" => descending ? filtered.OrderByDescending(x => x.Equipment.InventoryNumber) : filtered.OrderBy(x => x.Equipment.InventoryNumber),
            "name" => descending ? filtered.OrderByDescending(x => x.Equipment.Name) : filtered.OrderBy(x => x.Equipment.Name),
            "status" => descending ? filtered.OrderByDescending(x => x.StatusName) : filtered.OrderBy(x => x.StatusName),
            "note" => descending ? filtered.OrderByDescending(x => x.Assignment.Note) : filtered.OrderBy(x => x.Assignment.Note),
            // No explicit sort requested: most-recent-first is the natural default, regardless of
            // Dir - same reasoning as EquipmentRequestsController.GetMine's default branch.
            _ => filtered.OrderByDescending(x => x.Assignment.AssignedAtUtc),
        };

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MyAssignmentDto(x.Assignment.Id, x.Equipment.Id, x.Equipment.InventoryNumber, x.Equipment.Name, x.StatusName, x.Assignment.AssignedAtUtc, x.Assignment.Note))
            .ToListAsync(ct);

        return Ok(new PagedResult<MyAssignmentDto>(items, totalCount, page, pageSize));
    }

    // Assign requires EquipmentStatusId == 1 (Na skladištu), not just "not Otpisano" - this single
    // check also blocks Na servisu/Nedostaje, which the guidelines' literal wording alone would
    // have left assignable (confirmed with the user during planning). The explicit
    // no-active-assignment check is kept in addition, as an independent guard against a manager
    // manually resetting EquipmentStatusId back to 1 via the general edit form while an assignment
    // is still open - the DB's filtered unique index is the real backstop either way, caught below.
    [HttpPost]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<AssignmentDto>> AssignEquipment([FromBody] AssignEquipmentDto request, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([request.EquipmentId], ct);
        if (equipment is null)
        {
            return Problem(detail: "Odabrana oprema ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (equipment.EquipmentStatusId != 1)
        {
            return Problem(detail: "Oprema nije na skladištu pa se ne može zadužiti.", statusCode: StatusCodes.Status409Conflict);
        }

        if (await _db.EquipmentAssignments.AnyAsync(a => a.EquipmentId == request.EquipmentId && a.AssignmentStatusId == 1, ct))
        {
            return Problem(detail: "Oprema je već zadužena.", statusCode: StatusCodes.Status409Conflict);
        }

        var employee = await _db.Employees.FindAsync([request.EmployeeId], ct);
        if (employee is null || !employee.IsActive)
        {
            return Problem(detail: "Odabrani zaposlenik ne postoji ili nije aktivan.", statusCode: StatusCodes.Status400BadRequest);
        }

        var assignment = new EquipmentAssignment
        {
            EquipmentId = request.EquipmentId,
            EmployeeId = request.EmployeeId,
            AssignmentStatusId = 1,
            AssignedAtUtc = DateTime.UtcNow,
            AssignedByUserId = _currentUser.UserId,
            Note = request.Note,
        };

        equipment.EquipmentStatusId = 2;
        _db.EquipmentAssignments.Add(assignment);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The pre-checks above close the common case; only the DB's filtered unique index
            // (UX_Assignments_OneActivePerEquipment) closes the real race between two concurrent
            // assign calls for the same equipment.
            return Problem(detail: "Oprema je u međuvremenu već zadužena.", statusCode: StatusCodes.Status409Conflict);
        }

        var statusName = await _db.AssignmentStatuses.Where(s => s.Id == assignment.AssignmentStatusId).Select(s => s.Name).SingleAsync(ct);
        var dto = new AssignmentDto(
            assignment.Id, equipment.Id, equipment.InventoryNumber, equipment.Name,
            employee.Id, employee.FirstName + " " + employee.LastName,
            assignment.AssignmentStatusId, statusName,
            assignment.AssignedAtUtc, assignment.ReturnedAtUtc,
            _currentUser.DisplayName, null, assignment.Note);

        return CreatedAtAction(nameof(GetHistory), new { equipmentId = equipment.Id }, dto);
    }

    [HttpPost("{id:int}/return")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> ReturnEquipment(int id, CancellationToken ct)
    {
        var assignment = await _db.EquipmentAssignments.FindAsync([id], ct);
        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.AssignmentStatusId != 1)
        {
            return Problem(detail: "Zaduženje više nije aktivno.", statusCode: StatusCodes.Status409Conflict);
        }

        var returnedAtUtc = DateTime.UtcNow;
        // Structurally can't happen - ReturnedAtUtc is always server-stamped UtcNow, never
        // client-supplied - but the guidelines state the rule explicitly, so it's checked anyway.
        if (returnedAtUtc < assignment.AssignedAtUtc)
        {
            return Problem(detail: "Povrat ne može biti prije datuma zaduženja.", statusCode: StatusCodes.Status400BadRequest);
        }

        assignment.AssignmentStatusId = 2;
        assignment.ReturnedAtUtc = returnedAtUtc;
        assignment.ClosedByUserId = _currentUser.UserId;

        var equipment = await _db.Equipment.FindAsync([assignment.EquipmentId], ct);
        if (equipment is not null)
        {
            equipment.EquipmentStatusId = 1;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("transfer")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> TransferEquipment([FromBody] TransferEquipmentDto request, CancellationToken ct)
    {
        var equipment = await _db.Equipment.FindAsync([request.EquipmentId], ct);
        if (equipment is null)
        {
            return Problem(detail: "Odabrana oprema ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        var currentAssignment = await _db.EquipmentAssignments
            .SingleOrDefaultAsync(a => a.EquipmentId == request.EquipmentId && a.AssignmentStatusId == 1, ct);
        if (currentAssignment is null)
        {
            return Problem(detail: "Oprema trenutačno nije zadužena pa se ne može premjestiti.", statusCode: StatusCodes.Status409Conflict);
        }

        if (currentAssignment.EmployeeId == request.NewEmployeeId)
        {
            return Problem(detail: "Oprema je već zadužena tom zaposleniku.", statusCode: StatusCodes.Status400BadRequest);
        }

        var newEmployee = await _db.Employees.FindAsync([request.NewEmployeeId], ct);
        if (newEmployee is null || !newEmployee.IsActive)
        {
            return Problem(detail: "Odabrani zaposlenik ne postoji ili nije aktivan.", statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        currentAssignment.AssignmentStatusId = 3;
        currentAssignment.ReturnedAtUtc = now;
        currentAssignment.ClosedByUserId = _currentUser.UserId;

        var newAssignment = new EquipmentAssignment
        {
            EquipmentId = request.EquipmentId,
            EmployeeId = request.NewEmployeeId,
            AssignmentStatusId = 1,
            AssignedAtUtc = now,
            AssignedByUserId = _currentUser.UserId,
            Note = request.Note,
        };
        _db.EquipmentAssignments.Add(newAssignment);

        // Equipment.EquipmentStatusId is left untouched here - it stays Zaduženo throughout a
        // transfer, since the item never left circulation, just changed hands.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Problem(detail: "Oprema je u međuvremenu već premještena ili zadužena.", statusCode: StatusCodes.Status409Conflict);
        }

        return NoContent();
    }
}
