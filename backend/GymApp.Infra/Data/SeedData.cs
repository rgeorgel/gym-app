using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Infra.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Super admin
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin))
        {
            db.Users.Add(new User
            {
                Name = "Super Admin",
                Email = "admin@gymapp.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = UserRole.SuperAdmin,
                TenantId = null
            });
            await db.SaveChangesAsync();
        }

        // Demo tenant: Boxe Elite
        if (!await db.Tenants.AnyAsync(t => t.Slug == "boxe-elite"))
        {
            var tenant = new Tenant
            {
                Name = "Boxe Elite",
                Slug = "boxe-elite",
                PrimaryColor = "#1a1a2e",
                SecondaryColor = "#e94560",
                Plan = TenantPlan.Basic
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            // Admin da academia
            var admin = new User
            {
                TenantId = tenant.Id,
                Name = "Admin Boxe Elite",
                Email = "admin@boxe-elite.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = UserRole.Admin
            };
            db.Users.Add(admin);

            // Tipos de aula
            var grupoClass = new ClassType { TenantId = tenant.Id, Name = "Boxe em Grupo", Color = "#e94560", ModalityType = ModalityType.Group };
            var individualClass = new ClassType { TenantId = tenant.Id, Name = "Aula Individual", Color = "#f39c12", ModalityType = ModalityType.Individual };
            var duplaClass = new ClassType { TenantId = tenant.Id, Name = "Aula em Dupla", Color = "#27ae60", ModalityType = ModalityType.Pair };
            db.ClassTypes.AddRange(grupoClass, individualClass, duplaClass);
            await db.SaveChangesAsync();

            // Grade semanal (segunda a sábado, 7h e 19h para grupo)
            var schedules = new List<Schedule>
            {
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 1, StartTime = new TimeOnly(7,0), Capacity = 20 },   // Segunda 7h
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 1, StartTime = new TimeOnly(19,0), Capacity = 20 },  // Segunda 19h
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 3, StartTime = new TimeOnly(7,0), Capacity = 20 },   // Quarta 7h
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 3, StartTime = new TimeOnly(19,0), Capacity = 20 },  // Quarta 19h
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 5, StartTime = new TimeOnly(7,0), Capacity = 20 },   // Sexta 7h
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 5, StartTime = new TimeOnly(19,0), Capacity = 20 },  // Sexta 19h
                new() { TenantId = tenant.Id, ClassTypeId = grupoClass.Id, Weekday = 6, StartTime = new TimeOnly(9,0), Capacity = 25 },   // Sábado 9h
            };
            db.Schedules.AddRange(schedules);

            // Aluno demo
            var student = new User
            {
                TenantId = tenant.Id,
                Name = "João Silva",
                Email = "joao@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("aluno123"),
                Role = UserRole.Student,
                Phone = "(11) 99999-1234",
                BirthDate = new DateOnly(1990, 5, 15)
            };
            db.Users.Add(student);
            await db.SaveChangesAsync();

            // Pacote demo para o aluno
            var package = new Package
            {
                TenantId = tenant.Id,
                StudentId = student.Id,
                Name = "Plano Março 2026",
                ExpiresAt = new DateOnly(2026, 3, 31)
            };
            package.Items.Add(new PackageItem { ClassTypeId = grupoClass.Id, TotalCredits = 12, PricePerCredit = 50m });
            package.Items.Add(new PackageItem { ClassTypeId = individualClass.Id, TotalCredits = 4, PricePerCredit = 120m });
            package.Items.Add(new PackageItem { ClassTypeId = duplaClass.Id, TotalCredits = 2, PricePerCredit = 80m });
            db.Packages.Add(package);

            await db.SaveChangesAsync();
        }
    }
}
