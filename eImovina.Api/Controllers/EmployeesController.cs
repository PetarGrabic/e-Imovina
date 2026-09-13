using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Shared.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

// No GET-list and no delete/deactivate - only ever reached from the Admin-only Users page's
// "create/edit linked employee" dialogs (Section 15 post-handoff), not a standalone roster page.
[ApiController]
[Route("api/employees")]
[Authorize(Policy = "AdminOnly")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmployeesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDetailDto>> GetEmployeeById(int id, CancellationToken ct)
    {
        var dto = await GetDetailAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDetailDto>> CreateEmployee([FromBody] CreateEmployeeDto request, CancellationToken ct)
    {
        if (!await _db.Locations.AnyAsync(l => l.Id == request.LocationId, ct))
        {
            return Problem(detail: "Odabrana lokacija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        var employee = new Employee
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            LocationId = request.LocationId,
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(employee.Id, ct);
        return CreatedAtAction(nameof(GetEmployeeById), new { id = employee.Id }, dto);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDetailDto>> UpdateEmployee(int id, [FromBody] UpdateEmployeeDto request, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync([id], ct);
        if (employee is null)
        {
            return NotFound();
        }

        if (!await _db.Locations.AnyAsync(l => l.Id == request.LocationId, ct))
        {
            return Problem(detail: "Odabrana lokacija ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.LocationId = request.LocationId;

        await _db.SaveChangesAsync(ct);

        return Ok(await GetDetailAsync(id, ct));
    }

    private async Task<EmployeeDetailDto?> GetDetailAsync(int id, CancellationToken ct) =>
        await (
            from e in _db.Employees.AsNoTracking()
            join l in _db.Locations.AsNoTracking() on e.LocationId equals l.Id
            where e.Id == id
            select new EmployeeDetailDto(e.Id, e.FirstName, e.LastName, e.LocationId, l.Name, e.IsActive)
        ).SingleOrDefaultAsync(ct);
}
