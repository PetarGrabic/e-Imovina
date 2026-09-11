using System.Security.Cryptography;
using eImovina.App.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace eImovina.App.Services;

/// <summary>
/// Reads the raw bearer token out of the signed-in cookie principal's protected "api_token" claim
/// and unprotects it. The token itself never appears anywhere else - not in page HTML, not in
/// browser storage - only inside this server-side call chain.
/// </summary>
public class TokenAccessor : ITokenAccessor
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IDataProtector _protector;

    public TokenAccessor(IHttpContextAccessor accessor, IDataProtectionProvider dataProtectionProvider)
    {
        _accessor = accessor;
        _protector = dataProtectionProvider.CreateProtector(AccountEndpoints.TokenProtectorPurpose);
    }

    public Task<string?> GetTokenAsync()
    {
        var protectedToken = _accessor.HttpContext?.User.FindFirst(AccountEndpoints.TokenClaimType)?.Value;
        if (string.IsNullOrEmpty(protectedToken))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            return Task.FromResult<string?>(_protector.Unprotect(protectedToken));
        }
        catch (CryptographicException)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
