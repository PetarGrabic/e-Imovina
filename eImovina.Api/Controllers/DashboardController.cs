using eImovina.Api.Data;
using eImovina.Api.Services;
using eImovina.Shared.Common;
using eImovina.Shared.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    // "Still needs action": Zaprimljeno(1)/U obradi(2)/Odobreno(3) - confirmed with the user
    // (PROJECT_PLAN.md Section 14 Notes), mirrors the write-off "pending" convention already used
    // in EquipmentController.IsEquipmentReferencedAsync.
    private static readonly int[] OpenRequestStatusIds = [1, 2, 3];

    // Write-off counterpart to OpenRequestStatusIds, combined into the same "Otvoreni zahtjevi"
    // tile - confirmed with the user post-handoff. Zaprimljeno(1)/Odobreno(3) are still
    // actionable (Odobreno still needs an Execute); Odbijeno(4)/Provedeno(5) are final.
    // WriteOffRequestStatusId 2 ("U obradi") is unreachable through any existing action by
    // design (DecideWriteOffRequestDto's [Range(3,4)] - Section 13's deliberate two-outcome
    // decision, not a multi-step approval flow), so it's omitted here, not overlooked.
    private static readonly int[] OpenWriteOffStatusIds = [1, 3];

    // "Open" = not yet locked: Nacrt(1)/Otvorena(2)/U tijeku(3)/Zavrsena(4) - confirmed with the
    // user post-handoff, replacing the original "U tijeku only" definition. Applied identically to
    // the manager-wide tile and the LocationResponsible personal tile below.
    private static readonly int[] OpenInventoryStatusIds = [1, 2, 3, 4];

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DashboardController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<ActionResult<DashboardSummaryDto>> GetDashboard(CancellationToken ct)
    {
        var equipment = _db.Equipment.AsNoTracking().Where(e => !e.IsArchived);
        var totalEquipmentCount = await equipment.CountAsync(ct);
        var assignedEquipmentCount = await equipment.CountAsync(e => e.EquipmentStatusId == 2, ct);
        var onServiceCount = await equipment.CountAsync(e => e.EquipmentStatusId == 3, ct);
        var missingCount = await equipment.CountAsync(e => e.EquipmentStatusId == 4, ct);
        // Raw sum regardless of Currency - no FX/Currencies table exists anywhere in this codebase
        // (Section 2's notes: a fixed ISO-4217 code was a deliberate simplification, not a full
        // multi-currency model).
        var totalRecordedValue = await equipment.SumAsync(e => (decimal?)e.PurchaseValue, ct) ?? 0m;

        // Combines EquipmentRequests and WriteOffRequests into the one "Otvoreni zahtjevi" tile -
        // both are "requests" in the app (confirmed with the user post-handoff).
        var openEquipmentRequestsCount = await _db.EquipmentRequests.AsNoTracking()
            .CountAsync(r => OpenRequestStatusIds.Contains(r.RequestStatusId), ct);
        var openWriteOffRequestsCount = await _db.WriteOffRequests.AsNoTracking()
            .CountAsync(w => OpenWriteOffStatusIds.Contains(w.WriteOffRequestStatusId), ct);
        var openRequestsCount = openEquipmentRequestsCount + openWriteOffRequestsCount;

        var openInventoriesCount = await _db.Inventories.AsNoTracking()
            .CountAsync(i => OpenInventoryStatusIds.Contains(i.InventoryStatusId), ct);

        // EF Core's Sqlite provider can't translate a GroupBy projected straight into a record
        // constructor call - group into an anonymous type first, materialize, then map to the DTO
        // client-side.
        var valueByLocationRaw = await (
            from e in _db.Equipment.AsNoTracking()
            join l in _db.Locations.AsNoTracking() on e.CurrentLocationId equals l.Id
            where !e.IsArchived
            group e by new { l.Id, l.Name } into g
            select new { g.Key.Id, g.Key.Name, TotalValue = g.Sum(e => (decimal?)e.PurchaseValue) ?? 0m })
            .OrderByDescending(x => x.TotalValue)
            .ToListAsync(ct);
        var valueByLocation = valueByLocationRaw.Select(x => new LocationValueDto(x.Id, x.Name, x.TotalValue)).ToList();

        var recentChanges = await GetRecentChangesAsync(ct);

        var dto = new DashboardSummaryDto(
            totalEquipmentCount, assignedEquipmentCount, onServiceCount, missingCount,
            openRequestsCount, openInventoriesCount, totalRecordedValue, valueByLocation, recentChanges);

        return Ok(dto);
    }

    // Identity comes ONLY from ICurrentUser.EmployeeId (a JWT claim) - never a query/body id, same
    // convention as every other /mine endpoint. An account with no linked employee returns zero
    // counts, not an error.
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<MyDashboardDto>> GetMyDashboard(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId is null)
        {
            return Ok(new MyDashboardDto(0, 0, 0));
        }

        var currentAssignmentsCount = await _db.EquipmentAssignments.AsNoTracking()
            .CountAsync(a => a.EmployeeId == employeeId && a.AssignmentStatusId == 1, ct);

        // Write-offs are submitted only by managers (WriteOffRequestsController is entirely
        // InventoryManagement-policy), so an employee never has "their own" write-off to count -
        // this stays equipment-requests-only, unlike GetDashboard's combined tile.
        var openRequestsCount = await _db.EquipmentRequests.AsNoTracking()
            .CountAsync(r => r.RequesterEmployeeId == employeeId && OpenRequestStatusIds.Contains(r.RequestStatusId), ct);

        // Inventory visibility is LocationResponsible-only - a plain Employee shouldn't see
        // location inventory data, matching the guidelines' scoping of Employee to their own
        // equipment/requests only.
        var openInventoriesAtMyLocationCount = 0;
        if (_currentUser.IsInRole(RoleNames.LocationResponsible))
        {
            var locationId = await _db.Employees.Where(e => e.Id == employeeId)
                .Select(e => (int?)e.LocationId).SingleOrDefaultAsync(ct);
            if (locationId is int resolvedLocationId)
            {
                openInventoriesAtMyLocationCount = await _db.Inventories.AsNoTracking()
                    .CountAsync(i => i.LocationId == resolvedLocationId && OpenInventoryStatusIds.Contains(i.InventoryStatusId), ct);
            }
        }

        return Ok(new MyDashboardDto(currentAssignmentsCount, openRequestsCount, openInventoriesAtMyLocationCount));
    }

    // "Last 5 important changes", sourced from the business history the guidelines already
    // require (assignments/inventories/write-offs) - not a new audit log. Three small targeted
    // queries (each already Take(5)) rather than a SQL UNION, merged and re-sorted in memory, then
    // Take(5) overall - simpler than translating a heterogeneous UNION and still cheap (at most 15
    // rows read).
    private async Task<List<RecentChangeDto>> GetRecentChangesAsync(CancellationToken ct)
    {
        var assignmentChanges = await (
            from a in _db.EquipmentAssignments.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on a.EquipmentId equals e.Id
            where a.AssignmentStatusId != 4 // Stornirano - reserved/unused (Section 10 notes)
            orderby (a.ReturnedAtUtc ?? a.AssignedAtUtc) descending
            select new
            {
                Timestamp = a.ReturnedAtUtc ?? a.AssignedAtUtc,
                a.AssignmentStatusId,
                EquipmentId = (int?)e.Id,
                e.InventoryNumber,
                CoverFileId = _db.EquipmentFiles.Where(f => f.EquipmentId == e.Id && f.IsCoverImage).Select(f => (int?)f.Id).FirstOrDefault(),
            })
            .Take(5)
            .ToListAsync(ct);

        var writeOffChanges = await (
            from w in _db.WriteOffRequests.AsNoTracking()
            join e in _db.Equipment.AsNoTracking() on w.EquipmentId equals e.Id
            orderby (w.ExecutedAtUtc ?? w.DecisionAtUtc ?? w.CreatedAtUtc) descending
            select new
            {
                Timestamp = w.ExecutedAtUtc ?? w.DecisionAtUtc ?? w.CreatedAtUtc,
                w.WriteOffRequestStatusId,
                IsExecuted = w.ExecutedAtUtc != null,
                IsDecided = w.DecisionAtUtc != null,
                EquipmentId = (int?)e.Id,
                e.InventoryNumber,
                CoverFileId = _db.EquipmentFiles.Where(f => f.EquipmentId == e.Id && f.IsCoverImage).Select(f => (int?)f.Id).FirstOrDefault(),
            })
            .Take(5)
            .ToListAsync(ct);

        var inventoryChanges = await (
            from i in _db.Inventories.AsNoTracking()
            join l in _db.Locations.AsNoTracking() on i.LocationId equals l.Id
            where i.InventoryStatusId >= 2 // has reached at least Otvorena - has a meaningful timestamp
            orderby (i.LockedAtUtc ?? i.CompletedAtUtc ?? i.OpenedAtUtc) descending
            select new
            {
                Timestamp = i.LockedAtUtc ?? i.CompletedAtUtc ?? i.OpenedAtUtc ?? DateTime.MinValue,
                i.InventoryStatusId,
                l.Name,
            })
            .Take(5)
            .ToListAsync(ct);

        var merged = new List<RecentChangeDto>();

        foreach (var a in assignmentChanges)
        {
            var description = a.AssignmentStatusId switch
            {
                1 => $"Oprema {a.InventoryNumber} zadužena",
                2 => $"Oprema {a.InventoryNumber} vraćena",
                3 => $"Oprema {a.InventoryNumber} prenesena drugom zaposleniku",
                _ => $"Oprema {a.InventoryNumber} — promjena zaduženja",
            };
            merged.Add(new RecentChangeDto(a.Timestamp, description, a.EquipmentId, a.InventoryNumber, a.CoverFileId));
        }

        foreach (var w in writeOffChanges)
        {
            var description = w switch
            {
                { IsExecuted: true } => $"Oprema {w.InventoryNumber} otpisana",
                { IsDecided: true, WriteOffRequestStatusId: 3 } => $"Zahtjev za otpis opreme {w.InventoryNumber} odobren",
                { IsDecided: true, WriteOffRequestStatusId: 4 } => $"Zahtjev za otpis opreme {w.InventoryNumber} odbijen",
                _ => $"Podnesen zahtjev za otpis opreme {w.InventoryNumber}",
            };
            merged.Add(new RecentChangeDto(w.Timestamp, description, w.EquipmentId, w.InventoryNumber, w.CoverFileId));
        }

        foreach (var i in inventoryChanges)
        {
            var description = i.InventoryStatusId switch
            {
                5 => $"Inventura na lokaciji {i.Name} zaključana",
                4 => $"Inventura na lokaciji {i.Name} završena",
                _ => $"Inventura na lokaciji {i.Name} otvorena",
            };
            merged.Add(new RecentChangeDto(i.Timestamp, description, null, null, null));
        }

        return merged
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(5)
            .ToList();
    }
}
