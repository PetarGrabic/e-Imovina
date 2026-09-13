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

        // Splitsko-Dalmatinska županija - 4 locations (Ured/Škola/Skladište/one Terenska lokacija
        // with no equipment, see below). Two further Terenska lokacija entries (Ispostava Makarska,
        // Ispostava Imotski) were removed as excessive for a 5-employee demo set - their equipment
        // was repointed to Centralno skladište (see the equipment list below).
        var locations = new List<Location>
        {
            new() { Name = "Županijska zgrada - Split", Code = "LOC-001", LocationTypeId = 1 /*Ured*/, Address = "Vukovarska ulica 1, 21000 Split" },
            new() { Name = "OŠ Manuš", Code = "LOC-002", LocationTypeId = 2 /*Škola*/, Address = "Manuška poljana 1, 21000 Split" },
            new() { Name = "Centralno skladište", Code = "LOC-003", LocationTypeId = 3 /*Skladište*/, Address = "Gospodarska zona Kaštel Sućurac, 21212 Kaštel Sućurac" },
            new() { Name = "Ispostava Sinj", Code = "LOC-004", LocationTypeId = 4 /*Terenska lokacija*/, Address = "Vrlička ulica 5, 21230 Sinj" },
        };
        db.Locations.AddRange(locations);
        await db.SaveChangesAsync(ct);

        // Ispostava Sinj (locations[3]) deliberately gets no equipment - Section 6's test relies on
        // one seeded location being referenced by an employee but not by any equipment.
        var employees = new List<Employee>
        {
            new() { FirstName = "Ivan", LastName = "Horvat", LocationId = locations[0].Id },
            new() { FirstName = "Ana", LastName = "Kovačić", LocationId = locations[1].Id },
            new() { FirstName = "Marko", LastName = "Novak", LocationId = locations[2].Id },
            new() { FirstName = "Petra", LastName = "Babić", LocationId = locations[3].Id },
            // Plain Employee-only demo account (alongside Ivan) now that Petra also holds
            // LocationResponsible - keeps a distinct "no elevated role" ownership-denial example.
            new() { FirstName = "Luka", LastName = "Perić", LocationId = locations[0].Id },
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
            new() { InventoryNumber = "INV-0007", Name = "Uredska stolica", EquipmentCategoryId = 3, EquipmentStatusId = 1, CurrentLocationId = locations[2].Id, PurchaseValue = 90.00m, Currency = "EUR" },
            new() { InventoryNumber = "INV-0008", Name = "Multifunkcijski uređaj Canon", EquipmentCategoryId = 2, EquipmentStatusId = 2 /*Zaduženo*/, CurrentLocationId = locations[1].Id, PurchaseValue = 410.00m, Currency = "EUR" },
            new() { InventoryNumber = "INV-0009", Name = "Projektor Epson EB-X41", EquipmentCategoryId = 5 /*Ostalo*/, EquipmentStatusId = 1, CurrentLocationId = locations[1].Id, PurchaseValue = 480.00m, Currency = "EUR" },
            new() { InventoryNumber = "INV-0010", Name = "Službeno vozilo Škoda Octavia", EquipmentCategoryId = 5, EquipmentStatusId = 2 /*Zaduženo*/, CurrentLocationId = locations[2].Id, PurchaseValue = 18000.00m, Currency = "EUR" },
            new() { InventoryNumber = "INV-0011", Name = "Prijenosno računalo Lenovo ThinkPad", EquipmentCategoryId = 1, EquipmentStatusId = 5 /*Otpisano*/, CurrentLocationId = locations[2].Id },
            new() { InventoryNumber = "INV-0012", Name = "Bušilica Makita", EquipmentCategoryId = 4, EquipmentStatusId = 2 /*Zaduženo*/, CurrentLocationId = locations[2].Id, PurchaseValue = 140.00m, Currency = "EUR" },
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
        var anaUser = MakeUser("ana.kovacic", "ana.kovacic@e-imovina.hr", employeeId: employees[1].Id); // multi-role: LocationResponsible + Employee
        var markoUser = MakeUser("marko.novak", "marko.novak@e-imovina.hr", employeeId: employees[2].Id); // multi-role: LocationResponsible + Employee
        // Petra also holds LocationResponsible (below) so Ispostava Sinj has an eligible inventory
        // responsible person - Luka is the plain Employee-only demo account instead.
        var ivanUser = MakeUser("ivan.horvat", "ivan.horvat@e-imovina.hr", employeeId: employees[0].Id);
        var petraUser = MakeUser("petra.babic", "petra.babic@e-imovina.hr", employeeId: employees[3].Id);
        var lukaUser = MakeUser("luka.peric", "luka.peric@e-imovina.hr", employeeId: employees[4].Id);

        db.AppUsers.AddRange(admin, manager, anaUser, markoUser, ivanUser, petraUser, lukaUser);
        await db.SaveChangesAsync(ct);

        db.AppUserRoles.AddRange(
            new AppUserRole { AppUserId = admin.Id, AppRoleId = 1 },     // Admin
            new AppUserRole { AppUserId = manager.Id, AppRoleId = 2 },   // InventoryManager
            new AppUserRole { AppUserId = anaUser.Id, AppRoleId = 3 },   // LocationResponsible
            new AppUserRole { AppUserId = anaUser.Id, AppRoleId = 4 },   // + Employee (multi-role demo account)
            new AppUserRole { AppUserId = markoUser.Id, AppRoleId = 3 }, // LocationResponsible
            new AppUserRole { AppUserId = markoUser.Id, AppRoleId = 4 }, // + Employee (multi-role demo account)
            new AppUserRole { AppUserId = ivanUser.Id, AppRoleId = 4 },  // Employee
            new AppUserRole { AppUserId = petraUser.Id, AppRoleId = 3 }, // LocationResponsible
            new AppUserRole { AppUserId = petraUser.Id, AppRoleId = 4 }, // + Employee (multi-role demo account)
            new AppUserRole { AppUserId = lukaUser.Id, AppRoleId = 4 }   // Employee (plain, no elevated role)
        );

        // Real assignment history, not just current-state snapshots: INV-0003 shows a full
        // transfer chain (Marko -> Premješteno -> Ana -> Vraćeno) landing back on the shelf.
        db.EquipmentAssignments.AddRange(
            new EquipmentAssignment
            {
                EquipmentId = equipment[0].Id, // INV-0001
                EmployeeId = employees[0].Id, // Ivan
                AssignmentStatusId = 1, // Aktivno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-30),
                AssignedByUserId = manager.Id,
                Note = "Zaduženje prijenosnog računala",
            },
            new EquipmentAssignment
            {
                EquipmentId = equipment[1].Id, // INV-0002
                EmployeeId = employees[0].Id, // Ivan
                AssignmentStatusId = 2, // Vraćeno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-90),
                ReturnedAtUtc = DateTime.UtcNow.AddDays(-60),
                AssignedByUserId = manager.Id,
                ClosedByUserId = manager.Id,
                Note = "Vraćeno nakon zamjene računala.",
            },
            new EquipmentAssignment
            {
                EquipmentId = equipment[2].Id, // INV-0003
                EmployeeId = employees[2].Id, // Marko
                AssignmentStatusId = 3, // Premješteno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-120),
                ReturnedAtUtc = DateTime.UtcNow.AddDays(-60),
                AssignedByUserId = manager.Id,
                ClosedByUserId = manager.Id,
                Note = "Premješteno na Anu Kovačić.",
            },
            new EquipmentAssignment
            {
                EquipmentId = equipment[2].Id, // INV-0003
                EmployeeId = employees[1].Id, // Ana
                AssignmentStatusId = 2, // Vraćeno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-60),
                ReturnedAtUtc = DateTime.UtcNow.AddDays(-20),
                AssignedByUserId = manager.Id,
                ClosedByUserId = manager.Id,
                Note = "Vraćeno u centralno skladište.",
            },
            new EquipmentAssignment
            {
                EquipmentId = equipment[7].Id, // INV-0008
                EmployeeId = employees[1].Id, // Ana
                AssignmentStatusId = 1, // Aktivno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-10),
                AssignedByUserId = manager.Id,
                Note = "Zaduženje multifunkcijskog uređaja.",
            },
            new EquipmentAssignment
            {
                EquipmentId = equipment[9].Id, // INV-0010
                EmployeeId = employees[3].Id, // Petra
                AssignmentStatusId = 1, // Aktivno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-45),
                AssignedByUserId = manager.Id,
                Note = "Zaduženje službenog vozila.",
            },
            new EquipmentAssignment
            {
                EquipmentId = equipment[11].Id, // INV-0012
                EmployeeId = employees[2].Id, // Marko
                AssignmentStatusId = 1, // Aktivno
                AssignedAtUtc = DateTime.UtcNow.AddDays(-5),
                AssignedByUserId = manager.Id,
                Note = "Zaduženje bušilice.",
            }
        );

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

        db.WriteOffRequests.AddRange(
            new WriteOffRequest
            {
                EquipmentId = equipment[5].Id, // the "Nedostaje" monitor (INV-0006)
                SubmittedByUserId = manager.Id,
                WriteOffRequestStatusId = 1, // Zaprimljeno
                Reason = "Monitor nedostaje nakon selidbe ureda.",
            },
            new WriteOffRequest
            {
                EquipmentId = equipment[10].Id, // the "Otpisano" laptop (INV-0011)
                SubmittedByUserId = manager.Id,
                WriteOffRequestStatusId = 5, // Provedeno
                Reason = "Računalo neispravno, ekonomski neisplativ popravak.",
                DecisionByUserId = admin.Id,
                DecisionAtUtc = DateTime.UtcNow.AddDays(-15),
                DecisionNote = "Odobren otpis.",
                ExecutedAtUtc = DateTime.UtcNow.AddDays(-10),
            }
        );

        await db.SaveChangesAsync(ct);
    }
}
