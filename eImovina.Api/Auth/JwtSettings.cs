namespace eImovina.Api.Auth;

/// <summary>Bound from the "Jwt" config section. Real values live only in dotnet user-secrets.</summary>
public class JwtSettings
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480;
}
