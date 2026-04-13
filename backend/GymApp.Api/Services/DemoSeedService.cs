using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Services;

public class DemoSeedService(AppDbContext db)
{
    // ── Static demo data ────────────────────────────────────────────────────

    private static readonly string LockedPasswordHash =
        "$2a$11$AAAAAAAAAAAAAAAAAAAAAO7vgRFsMIl2bMN4Qj7SLDjJ9B9r9kWz."; // not a valid login

    private static readonly (string Name, string Email)[] SalonClients =
    [
        ("Ana Beatriz Souza",  "demo.cli01@demo.local"),
        ("Camila Rodrigues",   "demo.cli02@demo.local"),
        ("Fernanda Lima",      "demo.cli03@demo.local"),
        ("Juliana Pereira",    "demo.cli04@demo.local"),
        ("Mariana Costa",      "demo.cli05@demo.local"),
        ("Patrícia Alves",     "demo.cli06@demo.local"),
        ("Renata Oliveira",    "demo.cli07@demo.local"),
        ("Sandra Martins",     "demo.cli08@demo.local"),
        ("Thaís Fernandes",    "demo.cli09@demo.local"),
        ("Vanessa Santos",     "demo.cli10@demo.local"),
        ("Bianca Cardoso",     "demo.cli11@demo.local"),
        ("Larissa Gomes",      "demo.cli12@demo.local"),
        ("Natália Ribeiro",    "demo.cli13@demo.local"),
        ("Carolina Mendes",    "demo.cli14@demo.local"),
        ("Débora Pinto",       "demo.cli15@demo.local"),
        ("Aline Ferreira",     "demo.cli16@demo.local"),
        ("Bruna Nascimento",   "demo.cli17@demo.local"),
        ("Cintia Barbosa",     "demo.cli18@demo.local"),
        ("Daiana Moreira",     "demo.cli19@demo.local"),
        ("Elaine Teixeira",    "demo.cli20@demo.local"),
    ];

    private static readonly (string Name, string Email)[] GymClients =
    [
        ("João Silva",         "demo.aluno01@demo.local"),
        ("Rafael Costa",       "demo.aluno02@demo.local"),
        ("Pedro Oliveira",     "demo.aluno03@demo.local"),
        ("Lucas Ferreira",     "demo.aluno04@demo.local"),
        ("Gabriel Santos",     "demo.aluno05@demo.local"),
        ("Thiago Martins",     "demo.aluno06@demo.local"),
        ("Bruno Lima",         "demo.aluno07@demo.local"),
        ("Eduardo Rodrigues",  "demo.aluno08@demo.local"),
        ("Mateus Pereira",     "demo.aluno09@demo.local"),
        ("Vinícius Alves",     "demo.aluno10@demo.local"),
        ("Mariana Souza",      "demo.aluno11@demo.local"),
        ("Juliana Costa",      "demo.aluno12@demo.local"),
        ("Camila Ferreira",    "demo.aluno13@demo.local"),
        ("Letícia Santos",     "demo.aluno14@demo.local"),
        ("Beatriz Oliveira",   "demo.aluno15@demo.local"),
        ("Amanda Lima",        "demo.aluno16@demo.local"),
        ("Isabela Rodrigues",  "demo.aluno17@demo.local"),
        ("Yasmin Pereira",     "demo.aluno18@demo.local"),
        ("Larissa Alves",      "demo.aluno19@demo.local"),
        ("Natália Martins",    "demo.aluno20@demo.local"),
    ];

    private static readonly TimeOnly[] AppointmentTimes =
    [
        new(9, 0), new(10, 0), new(11, 0),
        new(14, 0), new(15, 0), new(16, 0), new(17, 0),
    ];

    // ── Public API ──────────────────────────────────────────────────────────

    public Task<bool> HasDemoDataAsync(Guid tenantId) =>
        db.DemoSeedLogs.AnyAsync(l => l.TenantId == tenantId);

    public async Task SeedAsync(Tenant tenant)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var logs = new List<DemoSeedLog>();

                if (tenant.TenantType == TenantType.BeautySalon)
                    await SeedBeautySalonAsync(tenant, logs);
                else
                    await SeedGymAsync(tenant, logs);

                db.DemoSeedLogs.AddRange(logs);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    public async Task RemoveAsync(Guid tenantId)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                async Task Del<T>(string entityType, Func<HashSet<Guid>, IQueryable<T>> query)
                    where T : class
                {
                    var ids = await db.DemoSeedLogs
                        .Where(l => l.TenantId == tenantId && l.EntityType == entityType)
                        .Select(l => l.EntityId)
                        .ToListAsync();

                    if (ids.Count == 0) return;
                    var set = ids.ToHashSet();
                    await query(set).ExecuteDeleteAsync();
                }

                // FK-safe deletion order
                await Del<Booking>("Booking",                   ids => db.Bookings.Where(x => ids.Contains(x.Id)));
                await Del<Session>("Session",                   ids => db.Sessions.Where(x => ids.Contains(x.Id)));
                await Del<PackageItem>("PackageItem",           ids => db.PackageItems.Where(x => ids.Contains(x.Id)));
                await Del<Package>("Package",                   ids => db.Packages.Where(x => ids.Contains(x.Id)));
                await Del<ProfessionalAvailability>("ProfessionalAvailability",
                                                                ids => db.ProfessionalAvailability.Where(x => ids.Contains(x.Id)));
                await Del<Schedule>("Schedule",                 ids => db.Schedules.Where(x => ids.Contains(x.Id)));

                // InstructorServices: composite key — delete via tracked InstructorIds
                var instructorIds = await db.DemoSeedLogs
                    .Where(l => l.TenantId == tenantId && l.EntityType == "Instructor")
                    .Select(l => l.EntityId).ToListAsync();
                if (instructorIds.Count > 0)
                {
                    var iSet = instructorIds.ToHashSet();
                    await db.InstructorServices.Where(x => iSet.Contains(x.InstructorId)).ExecuteDeleteAsync();
                }

                await Del<Instructor>("Instructor",             ids => db.Instructors.Where(x => ids.Contains(x.Id)));
                await Del<FinancialTransaction>("FinancialTransaction", ids => db.FinancialTransactions.Where(x => ids.Contains(x.Id)));
                await Del<Expense>("Expense",                   ids => db.Expenses.Where(x => ids.Contains(x.Id)));
                await Del<User>("User",                         ids => db.Users.Where(x => ids.Contains(x.Id)));

                // Cascade-safe: delete any Sessions/Bookings still referencing demo ClassTypes
                // (handles sessions not individually tracked, or created after the seed)
                var demoCtIds = await db.DemoSeedLogs
                    .Where(l => l.TenantId == tenantId && l.EntityType == "ClassType")
                    .Select(l => l.EntityId).ToListAsync();
                if (demoCtIds.Count > 0)
                {
                    var ctSet = demoCtIds.ToHashSet();
                    var orphanIds = await db.Sessions
                        .Where(s => s.ClassTypeId != null && ctSet.Contains(s.ClassTypeId.Value))
                        .Select(s => s.Id).ToListAsync();
                    if (orphanIds.Count > 0)
                    {
                        var osSet = orphanIds.ToHashSet();
                        await db.Bookings.Where(b => osSet.Contains(b.SessionId)).ExecuteDeleteAsync();
                        await db.Sessions.Where(s => s.ClassTypeId != null && ctSet.Contains(s.ClassTypeId.Value)).ExecuteDeleteAsync();
                    }
                }

                await Del<ClassType>("ClassType",               ids => db.ClassTypes.Where(x => ids.Contains(x.Id)));
                await Del<ServiceCategory>("ServiceCategory",   ids => db.ServiceCategories.Where(x => ids.Contains(x.Id)));
                await Del<Location>("Location",                 ids => db.Locations.Where(x => ids.Contains(x.Id)));

                await db.DemoSeedLogs.Where(l => l.TenantId == tenantId).ExecuteDeleteAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    // ── Beauty Salon seed ───────────────────────────────────────────────────

    private async Task SeedBeautySalonAsync(Tenant tenant, List<DemoSeedLog> logs)
    {
        var tid = tenant.Id;
        void Track(string type, Guid id) => logs.Add(new DemoSeedLog { TenantId = tid, EntityType = type, EntityId = id });

        // Location
        var location = await db.Locations.FirstOrDefaultAsync(l => l.TenantId == tid && l.IsMain);
        if (location is null)
        {
            location = new Location { TenantId = tid, Name = "[Demo] Salão Principal", IsMain = true };
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            Track("Location", location.Id);
        }

        // Service categories
        var catCabelo = new ServiceCategory { TenantId = tid, Name = "Cabelo", SortOrder = 0 };
        var catUnhas  = new ServiceCategory { TenantId = tid, Name = "Unhas & Estética", SortOrder = 1 };
        db.ServiceCategories.AddRange(catCabelo, catUnhas);
        await db.SaveChangesAsync();
        Track("ServiceCategory", catCabelo.Id);
        Track("ServiceCategory", catUnhas.Id);

        // Services
        ClassType[] services =
        [
            new() { TenantId = tid, Name = "Corte Feminino",         Color = "#e91e8c", ModalityType = ModalityType.Individual, DurationMinutes = 60,  Price = 80m,  CategoryId = catCabelo.Id },
            new() { TenantId = tid, Name = "Corte Masculino",         Color = "#2196f3", ModalityType = ModalityType.Individual, DurationMinutes = 30,  Price = 50m,  CategoryId = catCabelo.Id },
            new() { TenantId = tid, Name = "Coloração",               Color = "#9c27b0", ModalityType = ModalityType.Individual, DurationMinutes = 120, Price = 180m, CategoryId = catCabelo.Id },
            new() { TenantId = tid, Name = "Manicure",                Color = "#ff5722", ModalityType = ModalityType.Individual, DurationMinutes = 45,  Price = 35m,  CategoryId = catUnhas.Id  },
            new() { TenantId = tid, Name = "Pedicure",                Color = "#795548", ModalityType = ModalityType.Individual, DurationMinutes = 45,  Price = 40m,  CategoryId = catUnhas.Id  },
            new() { TenantId = tid, Name = "Design de Sobrancelha",   Color = "#607d8b", ModalityType = ModalityType.Individual, DurationMinutes = 30,  Price = 45m,  CategoryId = catUnhas.Id  },
        ];
        db.ClassTypes.AddRange(services);
        await db.SaveChangesAsync();
        foreach (var s in services) Track("ClassType", s.Id);

        // Professional users
        var userFernanda = NewUser(tid, "Fernanda Souza", "prof1@demo.local", UserRole.Admin);
        var userBeatriz  = NewUser(tid, "Beatriz Lima",   "prof2@demo.local", UserRole.Admin);
        db.Users.AddRange(userFernanda, userBeatriz);
        await db.SaveChangesAsync();
        Track("User", userFernanda.Id);
        Track("User", userBeatriz.Id);

        // Instructors
        var profFernanda = new Instructor { TenantId = tid, UserId = userFernanda.Id, PrimaryLocationId = location.Id, Bio = "Especialista em cortes e coloração", Specialties = "Corte, Coloração" };
        var profBeatriz  = new Instructor { TenantId = tid, UserId = userBeatriz.Id,  PrimaryLocationId = location.Id, Bio = "Especialista em unhas e estética",   Specialties = "Manicure, Pedicure, Sobrancelha" };
        db.Instructors.AddRange(profFernanda, profBeatriz);
        await db.SaveChangesAsync();
        Track("Instructor", profFernanda.Id);
        Track("Instructor", profBeatriz.Id);

        // InstructorServices (tracked via Instructor on removal)
        var fernandaServices = services.Where(s => s.CategoryId == catCabelo.Id)
            .Select(s => new InstructorService { InstructorId = profFernanda.Id, ClassTypeId = s.Id });
        var beatrizServices = services.Where(s => s.CategoryId == catUnhas.Id)
            .Select(s => new InstructorService { InstructorId = profBeatriz.Id, ClassTypeId = s.Id });
        db.InstructorServices.AddRange(fernandaServices.Concat(beatrizServices));

        // Professional availability Mon–Sat
        foreach (var prof in new[] { profFernanda, profBeatriz })
        {
            for (int wd = 1; wd <= 6; wd++)
            {
                var avail = new ProfessionalAvailability
                {
                    TenantId = tid, InstructorId = prof.Id,
                    Weekday = wd, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0),
                };
                db.ProfessionalAvailability.Add(avail);
                Track("ProfessionalAvailability", avail.Id);
            }
        }
        await db.SaveChangesAsync();

        // Demo clients
        var clients = SalonClients.Select(c => NewUser(tid, c.Name, c.Email, UserRole.Student)).ToArray();
        db.Users.AddRange(clients);
        await db.SaveChangesAsync();
        foreach (var c in clients) Track("User", c.Id);

        // Packages (1 per client, cycling 5 templates)
        (string name, ClassType service, int total, decimal price)[] pkgTemplates =
        [
            ("Pacote Corte Mensal",     services[0], 4,  80m),
            ("Pacote Coloração",        services[2], 3, 180m),
            ("Pacote Manicure",         services[3], 8,  35m),
            ("Pacote Pedicure",         services[4], 6,  40m),
            ("Pacote Sobrancelha",      services[5], 5,  45m),
        ];

        var expiry = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2);
        expiry = new DateOnly(expiry.Year, expiry.Month, DateTime.DaysInMonth(expiry.Year, expiry.Month));

        var packageMap = new Dictionary<Guid, PackageItem>(); // clientId → main PackageItem
        for (int i = 0; i < clients.Length; i++)
        {
            var tpl = pkgTemplates[i % pkgTemplates.Length];
            var pkg = new Package { TenantId = tid, StudentId = clients[i].Id, Name = tpl.name, ExpiresAt = expiry };
            var item = new PackageItem { PackageId = pkg.Id, ClassTypeId = tpl.service.Id, TotalCredits = tpl.total, UsedCredits = tpl.total / 2, PricePerCredit = tpl.price };
            db.Packages.Add(pkg);
            db.PackageItems.Add(item);
            Track("Package", pkg.Id);
            Track("PackageItem", item.Id);
            packageMap[clients[i].Id] = item;
        }
        await db.SaveChangesAsync();

        // Sessions + Bookings
        var instructors = new[] { profFernanda, profBeatriz };
        await CreateSessionsAndBookingsAsync(tid, location.Id, services, clients, instructors, packageMap, logs, isSalon: true);

        // Financial data
        await SeedFinancialAsync(tenant, clients, services, logs);
    }

    // ── Gym seed ────────────────────────────────────────────────────────────

    private async Task SeedGymAsync(Tenant tenant, List<DemoSeedLog> logs)
    {
        var tid = tenant.Id;
        void Track(string type, Guid id) => logs.Add(new DemoSeedLog { TenantId = tid, EntityType = type, EntityId = id });

        // Location
        var location = await db.Locations.FirstOrDefaultAsync(l => l.TenantId == tid && l.IsMain);
        if (location is null)
        {
            location = new Location { TenantId = tid, Name = "[Demo] Academia", IsMain = true };
            db.Locations.Add(location);
            await db.SaveChangesAsync();
            Track("Location", location.Id);
        }

        // Class types
        ClassType[] classTypes =
        [
            new() { TenantId = tid, Name = "Musculação", Color = "#f44336", ModalityType = ModalityType.Group, DurationMinutes = 60, Price = 60m },
            new() { TenantId = tid, Name = "Spinning",   Color = "#ff9800", ModalityType = ModalityType.Group, DurationMinutes = 45, Price = 50m },
            new() { TenantId = tid, Name = "Yoga",       Color = "#4caf50", ModalityType = ModalityType.Group, DurationMinutes = 60, Price = 45m },
            new() { TenantId = tid, Name = "Funcional",  Color = "#2196f3", ModalityType = ModalityType.Group, DurationMinutes = 50, Price = 55m },
        ];
        db.ClassTypes.AddRange(classTypes);
        await db.SaveChangesAsync();
        foreach (var ct in classTypes) Track("ClassType", ct.Id);

        // Instructors
        var userCarlos  = NewUser(tid, "Carlos Mendes",  "prof.gym1@demo.local", UserRole.Admin);
        var userRodrigo = NewUser(tid, "Rodrigo Santos", "prof.gym2@demo.local", UserRole.Admin);
        db.Users.AddRange(userCarlos, userRodrigo);
        await db.SaveChangesAsync();
        Track("User", userCarlos.Id);
        Track("User", userRodrigo.Id);

        var profCarlos  = new Instructor { TenantId = tid, UserId = userCarlos.Id,  PrimaryLocationId = location.Id, Specialties = "Musculação, Funcional" };
        var profRodrigo = new Instructor { TenantId = tid, UserId = userRodrigo.Id, PrimaryLocationId = location.Id, Specialties = "Spinning, Yoga" };
        db.Instructors.AddRange(profCarlos, profRodrigo);
        await db.SaveChangesAsync();
        Track("Instructor", profCarlos.Id);
        Track("Instructor", profRodrigo.Id);

        // InstructorServices
        db.InstructorServices.AddRange(
            new InstructorService { InstructorId = profCarlos.Id,  ClassTypeId = classTypes[0].Id },
            new InstructorService { InstructorId = profCarlos.Id,  ClassTypeId = classTypes[3].Id },
            new InstructorService { InstructorId = profRodrigo.Id, ClassTypeId = classTypes[1].Id },
            new InstructorService { InstructorId = profRodrigo.Id, ClassTypeId = classTypes[2].Id }
        );

        // Schedules
        (int classTypeIdx, int weekday, int hour, int capacity, int instructorIdx)[] scheduleDefs =
        [
            (0, 1, 7,  20, 0), // Musculação Seg 07:00 — Carlos
            (0, 4, 7,  20, 0), // Musculação Qui 07:00 — Carlos
            (1, 2, 8,  15, 1), // Spinning   Ter 08:00 — Rodrigo
            (1, 5, 8,  15, 1), // Spinning   Sex 08:00 — Rodrigo
            (2, 3, 9,  12, 1), // Yoga       Qua 09:00 — Rodrigo
            (2, 6, 9,  12, 1), // Yoga       Sáb 09:00 — Rodrigo
            (3, 1, 19, 20, 0), // Funcional  Seg 19:00 — Carlos
            (3, 4, 19, 20, 0), // Funcional  Qui 19:00 — Carlos
        ];
        var instructorPair = new[] { profCarlos, profRodrigo };
        var schedules = scheduleDefs.Select(d => new Schedule
        {
            TenantId = tid, ClassTypeId = classTypes[d.classTypeIdx].Id,
            InstructorId = instructorPair[d.instructorIdx].Id, LocationId = location.Id,
            Weekday = d.weekday, StartTime = new TimeOnly(d.hour, 0),
            DurationMinutes = classTypes[d.classTypeIdx].DurationMinutes ?? 60,
            Capacity = d.capacity,
        }).ToArray();
        db.Schedules.AddRange(schedules);
        await db.SaveChangesAsync();
        foreach (var s in schedules) Track("Schedule", s.Id);

        // Students
        var students = GymClients.Select(c => NewUser(tid, c.Name, c.Email, UserRole.Student)).ToArray();
        db.Users.AddRange(students);
        await db.SaveChangesAsync();
        foreach (var s in students) Track("User", s.Id);

        // Packages (1 per student, includes all class types)
        var expiry = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2);
        expiry = new DateOnly(expiry.Year, expiry.Month, DateTime.DaysInMonth(expiry.Year, expiry.Month));

        var packageMap = new Dictionary<Guid, PackageItem>();
        for (int i = 0; i < students.Length; i++)
        {
            var pkg = new Package { TenantId = tid, StudentId = students[i].Id, Name = "Plano Mensal Demo", ExpiresAt = expiry };
            // One PackageItem per class type; pick a rotating one as the "main" item for bookings
            db.Packages.Add(pkg);
            Track("Package", pkg.Id);

            PackageItem? mainItem = null;
            foreach (var ct in classTypes)
            {
                var item = new PackageItem { PackageId = pkg.Id, ClassTypeId = ct.Id, TotalCredits = 10, UsedCredits = 4, PricePerCredit = ct.Price ?? 50m };
                db.PackageItems.Add(item);
                Track("PackageItem", item.Id);
                if (mainItem is null || ct == classTypes[i % classTypes.Length]) mainItem = item;
            }
            packageMap[students[i].Id] = mainItem!;
        }
        await db.SaveChangesAsync();

        // Sessions + Bookings
        await CreateSessionsAndBookingsAsync(tid, location.Id, classTypes, students,
            instructorPair, packageMap, logs, isSalon: false);

        // Financial data
        await SeedFinancialAsync(tenant, students, classTypes, logs);
    }

    // ── Shared session/booking generation ───────────────────────────────────

    private async Task CreateSessionsAndBookingsAsync(
        Guid tid, Guid locationId,
        ClassType[] services, User[] clients,
        Instructor[] professionals,
        Dictionary<Guid, PackageItem> packageMap,
        List<DemoSeedLog> logs,
        bool isSalon)
    {
        void Track(string type, Guid id) => logs.Add(new DemoSeedLog { TenantId = tid, EntityType = type, EntityId = id });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var prevStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        int prevDays = DateTime.DaysInMonth(prevStart.Year, prevStart.Month);

        // Build list of (date, serviceIdx, professionalIdx, timeIdx) for appointments
        // Distribution: 3 appts per client in prev month + 2 in current month (past days) + 1 future
        var appointments = new List<(DateOnly date, int svcIdx, int profIdx, int timeIdx, bool isPast)>();

        int slot = 0;
        for (int ci = 0; ci < clients.Length; ci++)
        {
            // 3 appointments in previous month
            for (int a = 0; a < 3; a++)
            {
                int dayOffset = ((ci * 3 + a) * 3) % prevDays; // spread evenly
                var d = prevStart.AddDays(dayOffset);
                if (d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
                appointments.Add((d, (slot) % services.Length, slot % professionals.Length, slot % AppointmentTimes.Length, true));
                slot++;
            }
            // 2 appointments in current month (past)
            if (today.Day > 3)
            {
                for (int a = 0; a < 2; a++)
                {
                    int dayOffset = 1 + ((ci * 2 + a) * 2) % (today.Day - 1);
                    var d = new DateOnly(today.Year, today.Month, dayOffset);
                    if (d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
                    if (d >= today) d = d.AddDays(-1);
                    appointments.Add((d, (slot) % services.Length, slot % professionals.Length, slot % AppointmentTimes.Length, true));
                    slot++;
                }
            }
            // 1 future appointment
            {
                int daysAhead = 1 + (ci % 14);
                var d = today.AddDays(daysAhead);
                if (d.DayOfWeek == DayOfWeek.Sunday) d = d.AddDays(1);
                appointments.Add((d, (slot) % services.Length, slot % professionals.Length, slot % AppointmentTimes.Length, false));
                slot++;
            }
        }

        // For gym: group appointments sharing same (date, service, instructor) into one shared session
        // For salon: every appointment gets its own session (capacity 1)
        if (isSalon)
        {
            for (int i = 0; i < appointments.Count; i++)
            {
                var (date, svcIdx, profIdx, timeIdx, isPast) = appointments[i];
                var service = services[svcIdx];
                var prof = professionals[profIdx];
                var time = AppointmentTimes[timeIdx];
                var client = clients[i % clients.Length];
                var pkgItem = packageMap.TryGetValue(client.Id, out var pi) ? pi : null;

                var session = new Session
                {
                    TenantId = tid, ClassTypeId = service.Id, LocationId = locationId,
                    InstructorId = prof.Id, Date = date, StartTime = time,
                    DurationMinutes = service.DurationMinutes ?? 60, SlotsAvailable = 0,
                };
                db.Sessions.Add(session);
                Track("Session", session.Id);

                var bStatus = isPast
                    ? (i % 10 == 0 ? BookingStatus.Cancelled : BookingStatus.CheckedIn)
                    : BookingStatus.Confirmed;
                var booking = new Booking
                {
                    SessionId = session.Id, StudentId = client.Id,
                    PackageItemId = pkgItem?.Id, Status = bStatus,
                    CheckedInAt = bStatus == BookingStatus.CheckedIn
                        ? date.ToDateTime(time).AddMinutes(5).ToUniversalTime()
                        : null,
                    CancelledAt = bStatus == BookingStatus.Cancelled ? DateTime.UtcNow.AddDays(-1) : null,
                };
                db.Bookings.Add(booking);
                Track("Booking", booking.Id);
            }
        }
        else
        {
            // Gym: one session per (date, service, instructor), multiple students
            var sessionKey = new Dictionary<string, Session>();
            for (int i = 0; i < appointments.Count; i++)
            {
                var (date, svcIdx, profIdx, timeIdx, isPast) = appointments[i];
                var service = services[svcIdx];
                var prof = professionals[profIdx];
                var time = AppointmentTimes[timeIdx];
                var client = clients[i % clients.Length];
                var pkgItem = packageMap.TryGetValue(client.Id, out var pi) ? pi : null;

                var key = $"{date}|{service.Id}|{prof.Id}";
                if (!sessionKey.TryGetValue(key, out var session))
                {
                    session = new Session
                    {
                        TenantId = tid, ClassTypeId = service.Id, LocationId = locationId,
                        InstructorId = prof.Id, Date = date, StartTime = time,
                        DurationMinutes = service.DurationMinutes ?? 60, SlotsAvailable = 20,
                    };
                    db.Sessions.Add(session);
                    Track("Session", session.Id);
                    sessionKey[key] = session;
                }
                session.SlotsAvailable = Math.Max(0, session.SlotsAvailable - 1);

                var bStatus = isPast
                    ? (i % 10 == 0 ? BookingStatus.Cancelled : BookingStatus.CheckedIn)
                    : BookingStatus.Confirmed;
                var booking = new Booking
                {
                    SessionId = session.Id, StudentId = client.Id,
                    PackageItemId = pkgItem?.Id, Status = bStatus,
                    CheckedInAt = bStatus == BookingStatus.CheckedIn
                        ? date.ToDateTime(time).AddMinutes(5).ToUniversalTime()
                        : null,
                    CancelledAt = bStatus == BookingStatus.Cancelled ? DateTime.UtcNow.AddDays(-1) : null,
                };
                db.Bookings.Add(booking);
                Track("Booking", booking.Id);
            }
        }

        await db.SaveChangesAsync();
    }

    // ── Financial seed ──────────────────────────────────────────────────────

    private async Task SeedFinancialAsync(Tenant tenant, User[] clients, ClassType[] classTypes, List<DemoSeedLog> logs)
    {
        var tid = tenant.Id;
        void Track(string type, Guid id) => logs.Add(new DemoSeedLog { TenantId = tid, EntityType = type, EntityId = id });

        var today    = DateOnly.FromDateTime(DateTime.UtcNow);
        var prevM    = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        int prevDays = DateTime.DaysInMonth(prevM.Year, prevM.Month);
        bool isSalon = tenant.TenantType == TenantType.BeautySalon;

        // Package-like price per service (monthly total for gym, per-session for salon)
        static decimal TxAmount(ClassType ct, bool salon) =>
            salon ? (ct.Price ?? 50m) : Math.Round((ct.Price ?? 50m) * 4m, 2);

        int slot = 0;
        for (int i = 0; i < clients.Length; i++)
        {
            var client = clients[i];
            var ct     = classTypes[i % classTypes.Length];
            var amount = TxAmount(ct, isSalon);

            // Transaction in previous month
            int day = Math.Min(((i * 3 + 2) % (prevDays - 1)) + 1, prevDays);
            var (pm, feePct, inst) = PickPaymentMethod(slot++);
            var feeAmt = Math.Round(amount * feePct / 100m, 2);
            var prevTx = new FinancialTransaction
            {
                TenantId          = tid,
                Date              = prevM.AddDays(day - 1),
                StudentId         = client.Id,
                StudentName       = client.Name,
                ServiceName       = isSalon ? ct.Name : $"Plano Mensal – {ct.Name}",
                GrossAmount       = amount,
                PaymentMethod     = pm,
                Installments      = inst,
                CardFeePercentage = feePct,
                CardFeeAmount     = feeAmt,
                NetAmount         = amount - feeAmt,
            };
            db.FinancialTransactions.Add(prevTx);
            Track("FinancialTransaction", prevTx.Id);

            // Extra transaction for ~1/3 of clients in prev month (additional service)
            if (i % 3 == 1)
            {
                var ct2    = classTypes[(i + 1) % classTypes.Length];
                var amt2   = TxAmount(ct2, isSalon);
                int day2   = Math.Min(((i * 5 + 12) % (prevDays - 1)) + 1, prevDays);
                var (pm2, fee2, inst2) = PickPaymentMethod(slot++);
                var fee2Amt = Math.Round(amt2 * fee2 / 100m, 2);
                var prevTx2 = new FinancialTransaction
                {
                    TenantId          = tid,
                    Date              = prevM.AddDays(day2 - 1),
                    StudentId         = client.Id,
                    StudentName       = client.Name,
                    ServiceName       = isSalon ? ct2.Name : $"Plano Mensal – {ct2.Name}",
                    GrossAmount       = amt2,
                    PaymentMethod     = pm2,
                    Installments      = inst2,
                    CardFeePercentage = fee2,
                    CardFeeAmount     = fee2Amt,
                    NetAmount         = amt2 - fee2Amt,
                };
                db.FinancialTransactions.Add(prevTx2);
                Track("FinancialTransaction", prevTx2.Id);
            }

            // Transaction in current month for first 2/3 of clients
            if (today.Day >= 5 && i < (clients.Length * 2 / 3))
            {
                int curDay = Math.Min(((i * 2 + 1) % Math.Min(today.Day - 1, 28)) + 1, today.Day - 1);
                var (pm3, fee3, inst3) = PickPaymentMethod(slot++);
                var fee3Amt = Math.Round(amount * fee3 / 100m, 2);
                var curTx = new FinancialTransaction
                {
                    TenantId          = tid,
                    Date              = new DateOnly(today.Year, today.Month, curDay),
                    StudentId         = client.Id,
                    StudentName       = client.Name,
                    ServiceName       = isSalon ? ct.Name : $"Plano Mensal – {ct.Name}",
                    GrossAmount       = amount,
                    PaymentMethod     = pm3,
                    Installments      = inst3,
                    CardFeePercentage = fee3,
                    CardFeeAmount     = fee3Amt,
                    NetAmount         = amount - fee3Amt,
                };
                db.FinancialTransactions.Add(curTx);
                Track("FinancialTransaction", curTx.Id);
            }
        }

        await db.SaveChangesAsync();

        // Recurring expense templates — dated 2 months ago so the auto-gen system
        // creates copies for both the previous month and the current month
        var twoMonthsAgo = new DateOnly(today.Year, today.Month, 1).AddMonths(-2);

        (string Cat, string Desc, decimal Amt)[] recurring = isSalon
            ? [
                ("Locação",    "Aluguel do espaço",          2200m),
                ("Utilidades", "Água e Energia Elétrica",      280m),
                ("Serviços",   "Internet e Telefone",          150m),
                ("Software",   "Sistema de Agendamento",        99m),
              ]
            : [
                ("Locação",    "Aluguel da academia",         3500m),
                ("Utilidades", "Água e Energia Elétrica",      650m),
                ("Serviços",   "Internet e Telefone",          150m),
                ("Software",   "Sistema de Gestão",             99m),
              ];

        foreach (var (cat, desc, amt) in recurring)
        {
            var exp = new Expense { TenantId = tid, Date = twoMonthsAgo, Category = cat, Description = desc, Amount = amt, IsRecurring = true };
            db.Expenses.Add(exp);
            Track("Expense", exp.Id);
        }

        // One-time expenses in previous month
        (string Cat, string Desc, decimal Amt, int DayOff)[] oneTime = isSalon
            ? [
                ("Produtos",   "Tintas e produtos químicos",    420m, 5),
                ("Manutenção", "Cadeiras e lavatório",          280m, 12),
                ("Marketing",  "Anúncios e material gráfico",   220m, 18),
              ]
            : [
                ("Manutenção", "Revisão de equipamentos",       580m, 4),
                ("Suprimentos","Toalhas e produtos de limpeza", 220m, 11),
                ("Marketing",  "Anúncios em redes sociais",     300m, 20),
              ];

        foreach (var (cat, desc, amt, dayOff) in oneTime)
        {
            var exp = new Expense { TenantId = tid, Date = prevM.AddDays(dayOff), Category = cat, Description = desc, Amount = amt, IsRecurring = false };
            db.Expenses.Add(exp);
            Track("Expense", exp.Id);
        }

        await db.SaveChangesAsync();
    }

    private static (PaymentMethod Method, decimal FeePercent, int Installments) PickPaymentMethod(int idx) =>
        (idx % 6) switch
        {
            0 => (PaymentMethod.Pix,        0m,   1),
            1 => (PaymentMethod.Pix,        0m,   1),
            2 => (PaymentMethod.DebitCard,  1.5m, 1),
            3 => (PaymentMethod.CreditCard, 2.5m, 1),
            4 => (PaymentMethod.CreditCard, 3.5m, 3),
            _ => (PaymentMethod.Cash,       0m,   1),
        };

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static User NewUser(Guid tenantId, string name, string email, UserRole role) =>
        new()
        {
            TenantId = tenantId, Name = name, Email = email,
            PasswordHash = LockedPasswordHash, Role = role,
            Status = StudentStatus.Active,
        };
}
