using System.Net.Http.Headers;

namespace eImovina.App.Services;

/// <summary>
/// Attaches the signed-in user's bearer token to every outgoing ApiClient request. Scoped
/// (per-circuit), constructed with ITokenAccessor rather than IHttpContextAccessor directly so
/// the token lookup always reads fresh state instead of anything captured at construction time.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ITokenAccessor _tokenAccessor;

    public AuthHeaderHandler(ITokenAccessor tokenAccessor)
    {
        _tokenAccessor = tokenAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenAccessor.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
