using eImovina.Api.Data;
using eImovina.Api.Data.Entities;
using eImovina.Shared.Common;
using eImovina.Shared.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    // Fixed seeded ids (Section 3's "Do not renumber" convention) - same hardcode style as
    // EquipmentStatusId == 5 elsewhere in this codebase.
    private const int AdminRoleId = 1;
    private const int LocationResponsibleRoleId = 3;

    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListDto>>> GetUsers([FromQuery] UserQuery query, CancellationToken ct)
    {
        var filtered =
            from u in _db.AppUsers.AsNoTracking()
            join empJoin in _db.Employees.AsNoTracking() on u.EmployeeId equals empJoin.Id into empGroup
            from employee in empGroup.DefaultIfEmpty()
            join locJoin in _db.Locations.AsNoTracking() on (employee != null ? (int?)employee.LocationId : null) equals locJoin.Id into locGroup
            from location in locGroup.DefaultIfEmpty()
            select new
            {
                User = u,
                EmployeeName = employee != null ? employee.FirstName + " " + employee.LastName : null,
                EmployeeLocationId = employee != null ? (int?)employee.LocationId : null,
                EmployeeLocationName = location != null ? location.Name : null,
            };

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim();
            filtered = filtered.Where(x => EF.Functions.Like(x.User.UserName, $"%{text}%") || EF.Functions.Like(x.User.Email, $"%{text}%"));
        }

        if (query.IsActive is bool isActive)
        {
            filtered = filtered.Where(x => x.User.IsActive == isActive);
        }

        if (query.RoleId is int roleId)
        {
            filtered = filtered.Where(x => _db.AppUserRoles.Any(ur => ur.AppUserId == x.User.Id && ur.AppRoleId == roleId));
        }

        var descending = query.Dir == SortDirection.Descending;
        filtered = query.Sort?.ToLowerInvariant() switch
        {
            "email" => descending ? filtered.OrderByDescending(x => x.User.Email) : filtered.OrderBy(x => x.User.Email),
            "isactive" => descending ? filtered.OrderByDescending(x => x.User.IsActive) : filtered.OrderBy(x => x.User.IsActive),
            "employee" => descending ? filtered.OrderByDescending(x => x.EmployeeName) : filtered.OrderBy(x => x.EmployeeName),
            "location" => descending ? filtered.OrderByDescending(x => x.EmployeeLocationName) : filtered.OrderBy(x => x.EmployeeLocationName),
            "lastlogin" => descending ? filtered.OrderByDescending(x => x.User.LastLoginAtUtc) : filtered.OrderBy(x => x.User.LastLoginAtUtc),
            // Roles are multi-valued per user - sort by the alphabetically-first role name via a
            // correlated scalar subquery (same shape as EquipmentController's CoverFileId
            // subquery), not a GroupBy-into-constructor (that failed to translate on this Sqlite
            // provider during Section 14's dashboard work).
            "roles" => descending
                ? filtered.OrderByDescending(x =>
                    (from ur in _db.AppUserRoles
                     join r in _db.AppRoles on ur.AppRoleId equals r.Id
                     where ur.AppUserId == x.User.Id
                     orderby r.Name
                     select r.Name).FirstOrDefault())
                : filtered.OrderBy(x =>
                    (from ur in _db.AppUserRoles
                     join r in _db.AppRoles on ur.AppRoleId equals r.Id
                     where ur.AppUserId == x.User.Id
                     orderby r.Name
                     select r.Name).FirstOrDefault()),
            _ => descending ? filtered.OrderByDescending(x => x.User.UserName) : filtered.OrderBy(x => x.User.UserName),
        };

        var totalCount = await filtered.CountAsync(ct);
        var page = Math.Max(query.Page, 1);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var pageRows = await filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.User.Id, x.User.UserName, x.User.Email, x.User.IsActive,
                x.User.EmployeeId, x.EmployeeName, x.EmployeeLocationId, x.EmployeeLocationName,
                x.User.CreatedAtUtc, x.User.LastLoginAtUtc,
            })
            .ToListAsync(ct);

        // Roles-per-row can't be cheaply expressed as one SQL aggregate on the Sqlite provider, so
        // a second small query fetches roles for just this page's ids, grouped in C# - same
        // "several small AsNoTracking queries" style DashboardController/InventoryDetailDto use.
        var pageIds = pageRows.Select(x => x.Id).ToList();
        var roleRows = await (
            from ur in _db.AppUserRoles.AsNoTracking()
            join r in _db.AppRoles.AsNoTracking() on ur.AppRoleId equals r.Id
            where pageIds.Contains(ur.AppUserId)
            select new { ur.AppUserId, r.Name }
        ).ToListAsync(ct);
        var rolesByUserId = roleRows
            .GroupBy(x => x.AppUserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).ToList());

        var items = pageRows
            .Select(x => new UserListDto(
                x.Id, x.UserName, x.Email, x.IsActive,
                rolesByUserId.TryGetValue(x.Id, out var roles) ? roles : Array.Empty<string>(),
                x.EmployeeId, x.EmployeeName, x.EmployeeLocationId, x.EmployeeLocationName, x.CreatedAtUtc, x.LastLoginAtUtc))
            .ToList();

        return Ok(new PagedResult<UserListDto>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDetailDto>> GetUserById(int id, CancellationToken ct)
    {
        var dto = await GetDetailAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<UserDetailDto>> CreateUser([FromBody] CreateUserDto request, CancellationToken ct)
    {
        if (await _db.AppUsers.AnyAsync(u => u.UserName == request.UserName, ct))
        {
            return Problem(detail: $"Korisnik s korisničkim imenom '{request.UserName}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        if (await _db.AppUsers.AnyAsync(u => u.Email == request.Email, ct))
        {
            return Problem(detail: $"Korisnik s e-mailom '{request.Email}' već postoji.", statusCode: StatusCodes.Status409Conflict);
        }

        var roleIds = request.RoleIds.Distinct().ToList();
        var existingRoleCount = await _db.AppRoles.CountAsync(r => roleIds.Contains(r.Id), ct);
        if (existingRoleCount != roleIds.Count)
        {
            return Problem(detail: "Jedna ili više odabranih uloga ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (roleIds.Contains(LocationResponsibleRoleId) && request.EmployeeId is null)
        {
            return Problem(
                detail: "Uloga 'Odgovorna osoba lokacije' zahtijeva povezanog zaposlenika (zaposlenik uvijek ima lokaciju).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.EmployeeId is int employeeId)
        {
            var employeeError = await ValidateEmployeeLinkAsync(employeeId, excludeUserId: null, ct);
            if (employeeError is not null)
            {
                return employeeError;
            }
        }

        var user = new AppUser
        {
            UserName = request.UserName,
            Email = request.Email,
            EmployeeId = request.EmployeeId,
        };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, request.Password);

        _db.AppUsers.Add(user);
        // AppUserRole rows need the generated AppUserId, and AppUser has no navigation property to
        // fix that up automatically (no nav properties anywhere in this codebase) - a second
        // SaveChangesAsync is the simplest correct way to sequence it.
        await _db.SaveChangesAsync(ct);

        foreach (var roleId in roleIds)
        {
            _db.AppUserRoles.Add(new AppUserRole { AppUserId = user.Id, AppRoleId = roleId });
        }
        await _db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(user.Id, ct);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, dto);
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateUser(int id, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([id], ct);
        if (user is null)
        {
            return NotFound();
        }

        if (await IsLastActiveAdminAsync(user, ct))
        {
            return Problem(detail: "Ne možete deaktivirati posljednjeg aktivnog administratora.", statusCode: StatusCodes.Status409Conflict);
        }

        user.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Symmetric undo - no guard needed, mirrors EquipmentController.ReactivateEquipment's "nothing
    // else was invalidated by deactivation" reasoning.
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> ActivateUser(int id, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([id], ct);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:int}/roles")]
    public async Task<ActionResult<UserDetailDto>> UpdateUserRoles(int id, [FromBody] UpdateUserRolesDto request, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([id], ct);
        if (user is null)
        {
            return NotFound();
        }

        var roleIds = request.RoleIds.Distinct().ToList();
        var existingRoleCount = await _db.AppRoles.CountAsync(r => roleIds.Contains(r.Id), ct);
        if (existingRoleCount != roleIds.Count)
        {
            return Problem(detail: "Jedna ili više odabranih uloga ne postoji.", statusCode: StatusCodes.Status400BadRequest);
        }

        var currentlyAdmin = await _db.AppUserRoles.AnyAsync(ur => ur.AppUserId == id && ur.AppRoleId == AdminRoleId, ct);
        if (currentlyAdmin && !roleIds.Contains(AdminRoleId) && await IsLastActiveAdminAsync(user, ct))
        {
            return Problem(detail: "Ne možete ukloniti ulogu Administratora posljednjem aktivnom administratoru.", statusCode: StatusCodes.Status409Conflict);
        }

        // A LocationResponsible must always have a linked employee (which always has a location by
        // DB design) - this endpoint only touches roles, so a missing link can't be fixed in the
        // same call; the admin must link an employee first (via PUT {id}/employee).
        if (roleIds.Contains(LocationResponsibleRoleId) && user.EmployeeId is null)
        {
            return Problem(
                detail: "Uloga 'Odgovorna osoba lokacije' zahtijeva povezanog zaposlenika - prvo povežite zaposlenika s ovim računom.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var existingRoleRows = await _db.AppUserRoles.Where(ur => ur.AppUserId == id).ToListAsync(ct);
        _db.AppUserRoles.RemoveRange(existingRoleRows);
        foreach (var roleId in roleIds)
        {
            _db.AppUserRoles.Add(new AppUserRole { AppUserId = id, AppRoleId = roleId });
        }
        await _db.SaveChangesAsync(ct);

        return Ok(await GetDetailAsync(id, ct));
    }

    [HttpPut("{id:int}/employee")]
    public async Task<ActionResult<UserDetailDto>> UpdateUserEmployee(int id, [FromBody] LinkEmployeeDto request, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([id], ct);
        if (user is null)
        {
            return NotFound();
        }

        if (request.EmployeeId is int employeeId)
        {
            var employeeError = await ValidateEmployeeLinkAsync(employeeId, excludeUserId: id, ct);
            if (employeeError is not null)
            {
                return employeeError;
            }
        }
        else
        {
            // Unlinking - can't pull the location out from under an active LocationResponsible.
            // Swapping to a DIFFERENT employee (the `if` branch above) is fine since they still end
            // up linked to some employee, which always has a location.
            var isLocationResponsible = await _db.AppUserRoles.AnyAsync(ur => ur.AppUserId == id && ur.AppRoleId == LocationResponsibleRoleId, ct);
            if (isLocationResponsible)
            {
                return Problem(
                    detail: "Ne možete ukloniti zaposlenika dok korisnik ima ulogu 'Odgovorna osoba lokacije'.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        user.EmployeeId = request.EmployeeId;
        await _db.SaveChangesAsync(ct);

        return Ok(await GetDetailAsync(id, ct));
    }

    private async Task<UserDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var row = await (
            from u in _db.AppUsers.AsNoTracking()
            join empJoin in _db.Employees.AsNoTracking() on u.EmployeeId equals empJoin.Id into empGroup
            from employee in empGroup.DefaultIfEmpty()
            join locJoin in _db.Locations.AsNoTracking() on (employee != null ? (int?)employee.LocationId : null) equals locJoin.Id into locGroup
            from location in locGroup.DefaultIfEmpty()
            where u.Id == id
            select new
            {
                u.Id, u.UserName, u.Email, u.IsActive, u.EmployeeId,
                EmployeeName = employee != null ? employee.FirstName + " " + employee.LastName : null,
                EmployeeLocationId = employee != null ? (int?)employee.LocationId : null,
                EmployeeLocationName = location != null ? location.Name : null,
                u.CreatedAtUtc, u.LastLoginAtUtc,
            }
        ).SingleOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var roles = await (
            from ur in _db.AppUserRoles.AsNoTracking()
            join r in _db.AppRoles.AsNoTracking() on ur.AppRoleId equals r.Id
            where ur.AppUserId == id
            select r.Name
        ).ToListAsync(ct);

        return new UserDetailDto(
            row.Id, row.UserName, row.Email, row.IsActive, roles, row.EmployeeId, row.EmployeeName,
            row.EmployeeLocationId, row.EmployeeLocationName, row.CreatedAtUtc, row.LastLoginAtUtc);
    }

    // Shared by Create and UpdateUserEmployee - null return means "valid", otherwise the
    // ActionResult to return as-is.
    private async Task<ActionResult?> ValidateEmployeeLinkAsync(int employeeId, int? excludeUserId, CancellationToken ct)
    {
        var employee = await _db.Employees.FindAsync([employeeId], ct);
        if (employee is null || !employee.IsActive)
        {
            return Problem(detail: "Odabrani zaposlenik ne postoji ili nije aktivan.", statusCode: StatusCodes.Status400BadRequest);
        }

        var alreadyLinked = await _db.AppUsers.AnyAsync(u => u.EmployeeId == employeeId && u.Id != excludeUserId, ct);
        if (alreadyLinked)
        {
            return Problem(detail: "Odabrani zaposlenik je već povezan s drugim korisničkim računom.", statusCode: StatusCodes.Status409Conflict);
        }

        return null;
    }

    // "Last admin" and "self-lockout" are the same guard (confirmed with the user): block only
    // when the target user is CURRENTLY an active Admin and no OTHER active Admin exists. An admin
    // can deactivate/demote themselves freely as long as another active admin remains.
    private async Task<bool> IsLastActiveAdminAsync(AppUser user, CancellationToken ct)
    {
        if (!user.IsActive)
        {
            return false;
        }

        var isAdmin = await _db.AppUserRoles.AnyAsync(ur => ur.AppUserId == user.Id && ur.AppRoleId == AdminRoleId, ct);
        if (!isAdmin)
        {
            return false;
        }

        var otherActiveAdminExists = await (
            from ur in _db.AppUserRoles
            join u in _db.AppUsers on ur.AppUserId equals u.Id
            where ur.AppRoleId == AdminRoleId && u.IsActive && u.Id != user.Id
            select u.Id
        ).AnyAsync(ct);

        return !otherActiveAdminExists;
    }
}
