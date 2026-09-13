using eImovina.Api.Data;
using eImovina.Shared.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "InventoryManagement")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    // Aggregated across every Zaključana (5) inventory ever locked at a location - not just the
    // latest one - so a location that's been inventoried several times shows its accumulated
    // discrepancy pattern. Same 3-way discrepancy definition as
    // InventoriesController.GetDiscrepancies (not found / damaged / found-but-relocated).
    [HttpGet("discrepancies-by-location")]
    public async Task<ActionResult<List<LocationDiscrepancyDto>>> GetDiscrepanciesByLocation(CancellationToken ct)
    {
        var raw = await (
            from ii in _db.InventoryItems.AsNoTracking()
            join inv in _db.Inventories.AsNoTracking() on ii.InventoryId equals inv.Id
            join l in _db.Locations.AsNoTracking() on ii.ExpectedLocationId equals l.Id
            where inv.InventoryStatusId == 5
            group new { ii, inv } by new { l.Id, l.Name } into g
            select new
            {
                g.Key.Id,
                g.Key.Name,
                MissingCount = g.Count(x => x.ii.IsFound == false),
                DamagedCount = g.Count(x => x.ii.IsDamaged == true),
                RelocatedCount = g.Count(x => x.ii.IsFound == true && x.ii.FoundLocationId != null && x.ii.FoundLocationId != x.ii.ExpectedLocationId),
                InventoriesConsidered = g.Select(x => x.inv.Id).Distinct().Count(),
            })
            .OrderByDescending(x => x.MissingCount + x.DamagedCount + x.RelocatedCount)
            .ToListAsync(ct);

        var result = raw.Select(x => new LocationDiscrepancyDto(x.Id, x.Name, x.MissingCount, x.DamagedCount, x.RelocatedCount, x.InventoriesConsidered)).ToList();
        return Ok(result);
    }

    // Same LINQ-join-then-materialize workaround as DashboardController.GetDashboard's
    // ValueByLocation (EF Core's Sqlite provider can't translate a GroupBy projected straight into
    // a record constructor call).
    [HttpGet("value-by-category")]
    public async Task<ActionResult<List<CategoryValueDto>>> GetValueByCategory(CancellationToken ct)
    {
        var raw = await (
            from e in _db.Equipment.AsNoTracking()
            join c in _db.EquipmentCategories.AsNoTracking() on e.EquipmentCategoryId equals c.Id
            where !e.IsArchived
            group e by new { c.Id, c.Name } into g
            select new { g.Key.Id, g.Key.Name, TotalValue = g.Sum(e => (decimal?)e.PurchaseValue) ?? 0m })
            .OrderByDescending(x => x.TotalValue)
            .ToListAsync(ct);

        var result = raw.Select(x => new CategoryValueDto(x.Id, x.Name, x.TotalValue)).ToList();
        return Ok(result);
    }
}
