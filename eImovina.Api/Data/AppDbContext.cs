using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<LocationType> LocationTypes => Set<LocationType>();
    public DbSet<EquipmentCategory> EquipmentCategories => Set<EquipmentCategory>();
    public DbSet<EquipmentStatus> EquipmentStatuses => Set<EquipmentStatus>();
    public DbSet<AssignmentStatus> AssignmentStatuses => Set<AssignmentStatus>();
    public DbSet<InventoryStatus> InventoryStatuses => Set<InventoryStatus>();
    public DbSet<RequestStatus> RequestStatuses => Set<RequestStatus>();
    public DbSet<WriteOffRequestStatus> WriteOffRequestStatuses => Set<WriteOffRequestStatus>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<EquipmentAssignment> EquipmentAssignments => Set<EquipmentAssignment>();
    public DbSet<EquipmentLocationHistory> EquipmentLocationHistories => Set<EquipmentLocationHistory>();
    public DbSet<EquipmentStatusHistory> EquipmentStatusHistories => Set<EquipmentStatusHistory>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<EquipmentRequest> EquipmentRequests => Set<EquipmentRequest>();
    public DbSet<WriteOffRequest> WriteOffRequests => Set<WriteOffRequest>();
    public DbSet<EquipmentFile> EquipmentFiles => Set<EquipmentFile>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
