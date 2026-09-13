using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Api.Services;
using eImovina.Shared.Common;
using eImovina.Shared.EquipmentRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/equipmentrequests")]
public class EquipmentRequestsController : ControllerBase
{
    // Zaprimljeno(1) -> U obradi(2) -> Odobreno(3)/Odbijeno(4) -> Realizirano(5) -> Zatvoreno(6).
    // Zatvoreno is terminal; Odbijeno also closes out via Zatvoreno rather than Realizirano,
    // matching the guidelines' description of Zatvoreno as the general "administratively
    // completed" state. Confirmed with the user before implementation - see PROJECT_PLAN.md
    // Section 12 Notes.
    private static readonly Dictionary<int, int[]> AllowedTransitions = new()
    {
        [1] = [2],
        [2] = [3, 4],
        [3] = [5],
        [4] = [6],
        [5] = [6],
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public EquipmentRequestsController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private bool CallerIsManager() => _currentUser.IsInRole(RoleNames.Admin) || _currentUser.IsInRole(RoleNames.InventoryManager);

    private async Task<EquipmentRequestDetailDto?> GetDetailAsync(int id, CancellationToken ct) =>
        await (
            from r in _db.EquipmentRequests.AsNoTracking()
            join emp in _db.Employees.AsNoTracking() on r.RequesterEmployeeId equals emp.Id
            join cat in _db.EquipmentCategories.AsNoTracking() on r.EquipmentCategoryId equals cat.Id
            join s in _db.RequestStatuses.AsNoTracking() on r.RequestStatusId equals s.Id
            join decidedByUser in _db.AppUsers.AsNoTracking() on r.DecisionByUserId equals decidedByUser.Id into decidedByGroup
            from decidedBy in decidedByGroup.DefaultIfEmpty()
            where r.Id == id
            select new EquipmentRequestDetailDto(
                r.Id, r.RequesterEmployeeId, emp.FirstName + " " + emp.LastName,
                r.EquipmentCategoryId, cat.Name,
                r.RequestStatusId, s.Name,
                r.Description, r.CreatedAtUtc,
                r.DecisionByUserId, decidedBy != null ? decidedBy.UserName : null, r.DecisionAtUtc, r.DecisionNote)
        ).SingleOrDefaultAsync(ct);

    [HttpGet]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<PagedResult<EquipmentRequestListDto>>> GetEquipmentRequests([FromQuery] EquipmentRequestQuery query, CancellationToken ct)
    {
        var filtered =
            from r in _db.EquipmentRequests.AsNoTracking()
            join emp in _db.Employees.AsNoTracking() on r.RequesterEmployeeId equals emp.Id
            join cat in _db.EquipmentCategories.AsNoTracking() on r.EquipmentCategoryId equals cat.Id
            join s in _db.RequestStatuses.AsNoTracking() on r.RequestStatusId equals s.Id
            select new
            {
                Request = r,
                RequesterName = emp.FirstName + " " + emp.LastName,
                CategoryName = cat.Name,
                StatusName = s.Name,
            };

        if (query.RequestStatusId is int statusId)
        {
            filtered = filtered.Where(x => x.Request.RequestStatusId == statusId);
        }

        if (query.EquipmentCategoryId is int categoryId)
        {
            filtered = filtered.Where(x => x.Request.EquipmentCategoryId == categoryId);
        }

        if (query.RequesterEmployeeId is int employeeId)
        {
            filtered = filtered.Where(x => x.Request.RequesterEmployeeId == employeeId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x => EF.Functions.Like(x.Request.Description, $"%{text}%"));
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "requester" => descending ? filtered.OrderByDescending(x => x.RequesterName) : filtered.OrderBy(x => x.RequesterName),
            "category" => descending ? filtered.OrderByDescending(x => x.CategoryName) : filtered.OrderBy(x => x.CategoryName),
            "status" => descending ? filtered.OrderByDescending(x => x.StatusName) : filtered.OrderBy(x => x.StatusName),
            _ => filtered.OrderByDescending(x => x.Request.CreatedAtUtc),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var items = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EquipmentRequestListDto(
                x.Request.Id, x.Request.RequesterEmployeeId, x.RequesterName,
                x.Request.EquipmentCategoryId, x.CategoryName,
                x.Request.RequestStatusId, x.StatusName,
                x.Request.Description, x.Request.CreatedAtUtc, x.Request.DecisionAtUtc))
            .ToListAsync(ct);

        return Ok(new PagedResult<EquipmentRequestListDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<EquipmentRequestDetailDto>> GetById(int id, CancellationToken ct)
    {
        var dto = await GetDetailAsync(id, ct);
        if (dto is null)
        {
            return NotFound();
        }

        if (!CallerIsManager() && dto.RequesterEmployeeId != _currentUser.EmployeeId)
        {
            return Problem(detail: "Nemate pristup ovom zahtjevu.", statusCode: StatusCodes.Status403Forbidden);
        }

        return Ok(dto);
    }

    // Identity comes ONLY from ICurrentUser.EmployeeId (a JWT claim) - never a query/body id. An
    // account with no linked employee (Admin/InventoryManager) isn't an error case, it just has
    // nothing to show - same convention as AssignmentsController.GetMine.
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<List<EquipmentRequestListDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Ok(new List<EquipmentRequestListDto>());
        }

        var items = await (
            from r in _db.EquipmentRequests.AsNoTracking()
            join emp in _db.Employees.AsNoTracking() on r.RequesterEmployeeId equals emp.Id
            join cat in _db.EquipmentCategories.AsNoTracking() on r.EquipmentCategoryId equals cat.Id
            join s in _db.RequestStatuses.AsNoTracking() on r.RequestStatusId equals s.Id
            where r.RequesterEmployeeId == employeeId
            orderby r.CreatedAtUtc descending
            select new EquipmentRequestListDto(
                r.Id, r.RequesterEmployeeId, emp.FirstName + " " + emp.LastName,
                r.EquipmentCategoryId, cat.Name,
                r.RequestStatusId, s.Name,
                r.Description, r.CreatedAtUtc, r.DecisionAtUtc)
        ).ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("mine")]
    [Authorize]
    public async Task<ActionResult<EquipmentRequestDetailDto>> CreateMine([FromBody] CreateEquipmentRequestDto request, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Problem(detail: "Vaš račun nije povezan sa zaposlenikom pa ne možete poslati zahtjev.", statusCode: StatusCodes.Status400BadRequest);
        }

        var category = await _db.EquipmentCategories.FindAsync([request.EquipmentCategoryId], ct);
        if (category is null || !category.IsActive)
        {
            return Problem(detail: "Odabrana kategorija ne postoji ili nije aktivna.", statusCode: StatusCodes.Status400BadRequest);
        }

        var equipmentRequest = new EquipmentRequest
        {
            RequesterEmployeeId = employeeId.Value,
            EquipmentCategoryId = request.EquipmentCategoryId,
            RequestStatusId = 1,
            Description = request.Description,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _db.EquipmentRequests.Add(equipmentRequest);
        await _db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(equipmentRequest.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = equipmentRequest.Id }, dto);
    }

    [HttpPost("{id:int}/decide")]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<EquipmentRequestDetailDto>> Decide(int id, [FromBody] DecideEquipmentRequestDto request, CancellationToken ct)
    {
        var equipmentRequest = await _db.EquipmentRequests.FindAsync([id], ct);
        if (equipmentRequest is null)
        {
            return NotFound();
        }

        if (!AllowedTransitions.TryGetValue(equipmentRequest.RequestStatusId, out var allowedTargets) || !allowedTargets.Contains(request.RequestStatusId))
        {
            return Problem(detail: "Traženi prijelaz statusa nije dopušten.", statusCode: StatusCodes.Status409Conflict);
        }

        equipmentRequest.RequestStatusId = request.RequestStatusId;
        equipmentRequest.DecisionByUserId = _currentUser.UserId;
        equipmentRequest.DecisionAtUtc = DateTime.UtcNow;
        equipmentRequest.DecisionNote = request.DecisionNote;

        await _db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(id, ct);
        return Ok(dto);
    }
}
