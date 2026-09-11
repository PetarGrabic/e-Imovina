using eImovina.Api.Data.Entities;

namespace eImovina.Api.Services;

public interface ITokenService
{
    /// <summary>Mints a signed JWT for the given user + role names. Returns the token and its UTC expiry.</summary>
    (string Token, DateTime ExpiresAtUtc) CreateToken(AppUser user, IReadOnlyList<string> roles);
}
