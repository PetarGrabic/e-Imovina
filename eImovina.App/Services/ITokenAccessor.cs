namespace eImovina.App.Services;

/// <summary>Reads the current user's bearer token (unprotected) so AuthHeaderHandler can attach it to outgoing Api calls.</summary>
public interface ITokenAccessor
{
    Task<string?> GetTokenAsync();
}
