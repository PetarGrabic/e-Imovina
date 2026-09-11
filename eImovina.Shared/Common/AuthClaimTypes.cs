namespace eImovina.Shared.Common;

/// <summary>
/// Custom claim type literals shared between Api (mints them into the JWT) and App (forwards
/// them into the cookie principal at sign-in) that have no built-in System.Security.Claims.ClaimTypes
/// equivalent.
/// </summary>
public static class AuthClaimTypes
{
    public const string EmployeeId = "employeeId";
}
