using System.ComponentModel.DataAnnotations;
using eImovina.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

// TODO(Section 16): remove this controller or lock it down before submission.
[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHostEnvironment _env;

    public DiagnosticsController(AppDbContext db, IHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("db-summary")]
    public async Task<IActionResult> GetDbSummary(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(new
        {
            LocationTypes = await _db.LocationTypes.CountAsync(ct),
            EquipmentCategories = await _db.EquipmentCategories.CountAsync(ct),
            EquipmentStatuses = await _db.EquipmentStatuses.CountAsync(ct),
            AssignmentStatuses = await _db.AssignmentStatuses.CountAsync(ct),
            InventoryStatuses = await _db.InventoryStatuses.CountAsync(ct),
            RequestStatuses = await _db.RequestStatuses.CountAsync(ct),
            WriteOffRequestStatuses = await _db.WriteOffRequestStatuses.CountAsync(ct),
            AppRoles = await _db.AppRoles.CountAsync(ct),
            Locations = await _db.Locations.CountAsync(ct),
            Employees = await _db.Employees.CountAsync(ct),
            Equipment = await _db.Equipment.CountAsync(ct),
            EquipmentAssignments = await _db.EquipmentAssignments.CountAsync(ct),
            Inventories = await _db.Inventories.CountAsync(ct),
            InventoryItems = await _db.InventoryItems.CountAsync(ct),
            EquipmentRequests = await _db.EquipmentRequests.CountAsync(ct),
            WriteOffRequests = await _db.WriteOffRequests.CountAsync(ct),
            EquipmentFiles = await _db.EquipmentFiles.CountAsync(ct),
            AppUsers = await _db.AppUsers.CountAsync(ct),
            AppUserRoles = await _db.AppUserRoles.CountAsync(ct),
        });
    }

    // Throwaway endpoint exercising [ApiController]'s automatic ValidationProblemDetails 400 -
    // used by tests/section4.sh and the /dev/section4-scratch page to demonstrate ApiClient's
    // ProblemDetails -> ApiError mapping. No real business meaning.
    public sealed record EchoRequest([Required, MinLength(1)] string? Text);

    [HttpPost("echo")]
    public IActionResult PostEcho([FromBody] EchoRequest request) => Ok(request);
}
