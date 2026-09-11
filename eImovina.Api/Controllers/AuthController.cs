using eImovina.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eImovina.Api.Data.Entities;
using eImovina.Api.Services;
using eImovina.Shared.Auth;

namespace eImovina.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUser _currentUser;

    public AuthController(AppDbContext db, ITokenService tokenService, ICurrentUser currentUser)
    {
        _db = db;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _db.AppUsers.SingleOrDefaultAsync(u => u.UserName == request.UserName, ct);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "Neispravno korisničko ime ili lozinka." });
        }

        var hasher = new PasswordHasher<AppUser>();
        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Neispravno korisničko ime ili lozinka." });
        }

        var roles = await (
            from ur in _db.AppUserRoles
            join r in _db.AppRoles on ur.AppRoleId equals r.Id
            where ur.AppUserId == user.Id
            select r.Name).ToListAsync(ct);

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var (token, expiresAtUtc) = _tokenService.CreateToken(user, roles);

        return Ok(new LoginResponse(token, expiresAtUtc, user.UserName, roles, user.EmployeeId));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            _currentUser.UserId,
            _currentUser.DisplayName,
            _currentUser.Roles,
            _currentUser.EmployeeId,
        });
    }

    // Pure authz smoke-test endpoint - no business meaning. Used by this section's verification gate.
    [HttpGet("ping-admin")]
    [Authorize(Policy = "AdminOnly")]
    public IActionResult PingAdmin() => Ok(new { message = "pong" });
}
