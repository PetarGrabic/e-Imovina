namespace eImovina.Shared.Common;

/// <summary>
/// The 4 application roles, matching the seeded AppRoles.Name values exactly. Used for
/// [Authorize(Roles = ...)] policies and role-aware navigation from Section 5 on.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string InventoryManager = "InventoryManager";
    public const string LocationResponsible = "LocationResponsible";
    public const string Employee = "Employee";
}
