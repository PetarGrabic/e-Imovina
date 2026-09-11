using eImovina.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Data;

/// <summary>
/// Idempotent runtime seed for demo data (locations, employees, equipment, a sample assignment,
/// a draft inventory, sample requests, demo users for every role). Kept separate from the
/// lookup-table `HasData` seeding, which is migration-driven and separately idempotent -
/// this seeder needs computed password hashes and relational sample data that HasData can't
/// express with static values.
/// </summary>
public static class DemoDataSeeder
{
    private const string DemoPassword = "Demo123!";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Locations.AnyAsync(ct))
        {
            return; // demo data already present - do nothing.
        }

        var locations = new List<Location>
        {
            new() { Name = "Županijska zgrada - Sisak", Code = "LOC-001", LocationTypeId = 1 /*Ured*/ },
            new() { Name = "OŠ Ivan Kukuljević", Code = "LOC-002", LocationTypeId = 2 /*Škola*/ },
            new() { Name = "Centralno skladište", Code = "LOC-003", LocationTypeId = 3 /*Skladište*/ },
            new() { Name = "Ispostava Petrinja", Code = "LOC-004", LocationTypeId = 4 /*Terenska lokacija*/ },
        };
        db.Locations.AddRange(locations);
        await db.SaveChangesAsync(ct);

        var employees = new List<Employee>
        {
            new() { FirstName = "Ivan", LastName = "Horvat", Email = "ivan.horvat@e-imovina.hr", JobTitle = "Referent", LocationId = locations[0].Id },
            new() { FirstName = "Ana", LastName = "Kovačić", Email = "ana.kovacic@e-imovina.hr", JobTitle = "Ravnateljica", LocationId = locations[1].Id },
            new() { FirstName = "Marko", LastName = "Novak", Email = "marko.novak@e-imovina.hr", JobTitle = "Skladištar", LocationId = locations[2].Id },
            new() { FirstName = "Petra", LastName = "Babić", Email = "petra.babic@e-imovina.hr", JobTitle = "Terenski koordinator", LocationId = locations[3].Id },
        };
        db.Employees.AddRange(employees);
        await db.SaveChangesAsync(ct);

        var equipment = new List<Equipment>
        {
            new() { InventoryNumber = "INV-0001", Name = "Prijenosno računalo Dell Latitude 5540", EquipmentCategoryId = 1 /*Računalo*/, EquipmentStatusId = 2 /*Zaduženo*/, CurrentLocationId = locations[0].Id, PurchaseValue = 950.00m, Currency = "EUR", PurchaseDate = new DateOnly(2024, 3, 1) },
            new() { InventoryNumber = "INV-0002", Name = "Stolno računalo HP ProDesk", EquipmentCategoryId = 1, EquipmentStatusId = 1 /*Na skladištu*/, CurrentLocationId = locations[2].Id, PurchaseValue = 650.00m, Currency = "EUR" },
            new() { InventoryNumber = "INV-0003", Name = "Mrežni switch Cisco 24-port", EquipmentCategoryId = 2 /*Mrežna oprema*/, EquipmentStatusId = 1, CurrentLocationId = locations[2].Id, PurchaseValue = 320.00m, Currency = "EUR" },
            new() { InventoryNumber = "INV-0004", Name = "Uredski stol", EquipmentCategoryId = 3 /*Namještaj*/, EquipmentStatusId = 1, CurrentLocationId = locations[2].Id },
            new() { InventoryNumber = "INV-0005", Name = "Bušilica Bosch", EquipmentCategoryId = 4 /*Alat*/, EquipmentStatusId = 3 /*Na servisu*/, CurrentLocationId = locations[2].Id },
            new() { InventoryNumber = "INV-0006", Name = "Monitor Dell 24\"", EquipmentCategoryId = 1, EquipmentStatusId = 4 /*Nedostaje*/, CurrentLocationId = locations[1].Id },
        };
        db.Equipment.AddRange(equipment);
        await db.SaveChangesAsync(ct);

        var hasher = new PasswordHasher<AppUser>();

        AppUser MakeUser(string userName, string email, int? employeeId)
        {
            var user = new AppUser { UserName = userName, Email = email, EmployeeId = employeeId, IsActive = true };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        }

        var admin = MakeUser("admin", "admin@e-imovina.hr", employeeId: null);
        var manager = MakeUser("inventar.manager", "inventar.manager@e-imovina.hr", employeeId: null);
        var locResp = MakeUser("marko.novak", "marko.novak@e-imovina.hr", employeeId: employees[2].Id); // multi-role: LocationResponsible + Employee
        var employee = MakeUser("ivan.horvat", "ivan.horvat@e-imovina.hr", employeeId: employees[0].Id);

        db.AppUsers.AddRange(admin, manager, locResp, employee);
        await db.SaveChangesAsync(ct);

        db.AppUserRoles.AddRange(
            new AppUserRole { AppUserId = admin.Id, AppRoleId = 1 },   // Admin
            new AppUserRole { AppUserId = manager.Id, AppRoleId = 2 }, // InventoryManager
            new AppUserRole { AppUserId = locResp.Id, AppRoleId = 3 }, // LocationResponsible
            new AppUserRole { AppUserId = locResp.Id, AppRoleId = 4 }, // + Employee (multi-role demo account)
            new AppUserRole { AppUserId = employee.Id, AppRoleId = 4 } // Employee
        );

        db.EquipmentAssignments.Add(new EquipmentAssignment
        {
            EquipmentId = equipment[0].Id,
            EmployeeId = employees[0].Id,
            AssignmentStatusId = 1, // Aktivno
            AssignedAtUtc = DateTime.UtcNow.AddDays(-30),
            AssignedByUserId = manager.Id,
            Note = "Zaduženje prijenosnog računala",
        });

        db.Inventories.Add(new Inventory
        {
            LocationId = locations[2].Id,
            ResponsibleEmployeeId = employees[2].Id,
            InventoryStatusId = 1, // Nacrt - not yet opened, no items formed yet
            CreatedByUserId = manager.Id,
        });

        db.EquipmentRequests.AddRange(
            new EquipmentRequest { RequesterEmployeeId = employees[1].Id, EquipmentCategoryId = 1, RequestStatusId = 1 /*Zaprimljeno*/, Description = "Potreban dodatni monitor za nastavu." },
            new EquipmentRequest { RequesterEmployeeId = employees[0].Id, EquipmentCategoryId = 3, RequestStatusId = 3 /*Odobreno*/, Description = "Treba mi nova stolica.", DecisionByUserId = admin.Id, DecisionAtUtc = DateTime.UtcNow, DecisionNote = "Odobreno, nabava u tijeku." }
        );

        db.WriteOffRequests.Add(new WriteOffRequest
        {
            EquipmentId = equipment[5].Id, // the "Nedostaje" monitor
            SubmittedByUserId = manager.Id,
            WriteOffRequestStatusId = 1, // Zaprimljeno
            Reason = "Monitor nedostaje nakon selidbe ureda.",
        });

        await db.SaveChangesAsync(ct);
    }
}
