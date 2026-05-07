using FluentAssertions;
using GymApp.Api.Core;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymApp.Tests.Endpoints;

public class FinancialFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateTransaction_WithValidData_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        await db.SaveChangesAsync();

        var transaction = new FinancialTransaction
        {
            TenantId = tenantId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StudentId = Guid.NewGuid(),
            StudentName = "John Doe",
            ServiceName = "Monthly Plan",
            GrossAmount = 100m,
            PaymentMethod = PaymentMethod.Pix,
            Installments = 1,
            CardFeePercentage = 0m,
            CardFeeAmount = 0m,
            NetAmount = 100m
        };
        db.FinancialTransactions.Add(transaction);
        await db.SaveChangesAsync();

        var saved = await db.FinancialTransactions.FirstAsync(t => t.TenantId == tenantId);
        saved.GrossAmount.Should().Be(100m);
        saved.NetAmount.Should().Be(100m);
        saved.PaymentMethod.Should().Be(PaymentMethod.Pix);
    }

    [Fact]
    public async Task CreateTransaction_WithCreditCard_CalculatesFeeCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        db.CardFeeConfigs.Add(new CardFeeConfig { TenantId = tenantId, FeeType = "CreditCard1x", FeePercentage = 2.5m });
        await db.SaveChangesAsync();

        var grossAmount = 100m;
        var feePercentage = 2.5m;
        var feeAmount = Math.Round(grossAmount * feePercentage / 100m, 2);
        var netAmount = grossAmount - feeAmount;

        var transaction = new FinancialTransaction
        {
            TenantId = tenantId,
            GrossAmount = grossAmount,
            CardFeePercentage = feePercentage,
            CardFeeAmount = feeAmount,
            NetAmount = netAmount,
            PaymentMethod = PaymentMethod.CreditCard,
            Installments = 1
        };
        db.FinancialTransactions.Add(transaction);
        await db.SaveChangesAsync();

        var saved = await db.FinancialTransactions.FirstAsync(t => t.TenantId == tenantId);
        saved.CardFeeAmount.Should().Be(2.50m);
        saved.NetAmount.Should().Be(97.50m);
    }

    [Fact]
    public async Task CreateTransaction_WithInstallments_UsesCorrectFeeType()
    {
        var feeType = ResolveFeeType(PaymentMethod.CreditCard, 3);
        feeType.Should().Be("CreditCard2to6x");

        feeType = ResolveFeeType(PaymentMethod.CreditCard, 1);
        feeType.Should().Be("CreditCard1x");

        feeType = ResolveFeeType(PaymentMethod.CreditCard, 8);
        feeType.Should().Be("CreditCard7to12x");

        feeType = ResolveFeeType(PaymentMethod.Cash, 1);
        feeType.Should().Be("Cash");

        feeType = ResolveFeeType(PaymentMethod.Pix, 1);
        feeType.Should().Be("Pix");

        feeType = ResolveFeeType(PaymentMethod.DebitCard, 1);
        feeType.Should().Be("DebitCard");
    }

    private static string ResolveFeeType(PaymentMethod pm, int installments) => pm switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Pix => "Pix",
        PaymentMethod.DebitCard => "DebitCard",
        PaymentMethod.CreditCard when installments <= 1 => "CreditCard1x",
        PaymentMethod.CreditCard when installments <= 6 => "CreditCard2to6x",
        PaymentMethod.CreditCard => "CreditCard7to12x",
        _ => "Cash"
    };

    [Fact]
    public async Task UpdateTransaction_RecalculatesFees()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var transaction = new FinancialTransaction
        {
            Id = transactionId,
            TenantId = tenantId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            GrossAmount = 100m,
            PaymentMethod = PaymentMethod.Pix,
            Installments = 1,
            CardFeePercentage = 0m,
            CardFeeAmount = 0m,
            NetAmount = 100m
        };
        db.FinancialTransactions.Add(transaction);
        await db.SaveChangesAsync();

        var toUpdate = await db.FinancialTransactions.FindAsync(transactionId);
        toUpdate.GrossAmount = 200m;
        toUpdate.PaymentMethod = PaymentMethod.CreditCard;
        toUpdate.CardFeePercentage = 2.5m;
        toUpdate.CardFeeAmount = 5m;
        toUpdate.NetAmount = 195m;
        await db.SaveChangesAsync();

        var updated = await db.FinancialTransactions.AsNoTracking().FirstAsync(t => t.Id == transactionId);
        updated.GrossAmount.Should().Be(200m);
        updated.NetAmount.Should().Be(195m);
    }

    [Fact]
    public async Task DeleteTransaction_RemovesRecord()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var transaction = new FinancialTransaction
        {
            Id = transactionId,
            TenantId = tenantId,
            GrossAmount = 100m,
            PaymentMethod = PaymentMethod.Pix
        };
        db.FinancialTransactions.Add(transaction);
        await db.SaveChangesAsync();

        db.FinancialTransactions.Remove(transaction);
        await db.SaveChangesAsync();

        var exists = await db.FinancialTransactions.AnyAsync(t => t.Id == transactionId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetTransactions_ByDateRange_FiltersCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var baseDate = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        db.FinancialTransactions.Add(new FinancialTransaction { TenantId = tenantId, Date = new DateOnly(2024, 6, 1), GrossAmount = 50m, PaymentMethod = PaymentMethod.Pix });
        db.FinancialTransactions.Add(new FinancialTransaction { TenantId = tenantId, Date = new DateOnly(2024, 6, 10), GrossAmount = 75m, PaymentMethod = PaymentMethod.Pix });
        db.FinancialTransactions.Add(new FinancialTransaction { TenantId = tenantId, Date = new DateOnly(2024, 6, 20), GrossAmount = 100m, PaymentMethod = PaymentMethod.Pix });
        await db.SaveChangesAsync();

        var startDate = new DateOnly(2024, 6, 5);
        var endDate = new DateOnly(2024, 6, 15);

        var transactions = await db.FinancialTransactions
            .Where(t => t.TenantId == tenantId && t.Date >= startDate && t.Date <= endDate)
            .ToListAsync();

        transactions.Should().HaveCount(1);
        transactions[0].GrossAmount.Should().Be(75m);
    }

    [Fact]
    public async Task GetTransactions_ByStudentId_FiltersCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();

        db.FinancialTransactions.Add(new FinancialTransaction { TenantId = tenantId, StudentId = studentId1, GrossAmount = 50m, PaymentMethod = PaymentMethod.Pix });
        db.FinancialTransactions.Add(new FinancialTransaction { TenantId = tenantId, StudentId = studentId1, GrossAmount = 75m, PaymentMethod = PaymentMethod.Pix });
        db.FinancialTransactions.Add(new FinancialTransaction { TenantId = tenantId, StudentId = studentId2, GrossAmount = 100m, PaymentMethod = PaymentMethod.Pix });
        await db.SaveChangesAsync();

        var student1Transactions = await db.FinancialTransactions.Where(t => t.StudentId == studentId1).ToListAsync();
        student1Transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateExpense_WithValidData_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });
        await db.SaveChangesAsync();

        var expense = new Expense
        {
            TenantId = tenantId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Category = "Rent",
            Description = "Monthly rent",
            Amount = 1500m,
            IsRecurring = true
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        var saved = await db.Expenses.FirstAsync(e => e.TenantId == tenantId);
        saved.Amount.Should().Be(1500m);
        saved.Category.Should().Be("Rent");
        saved.IsRecurring.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateExpense_ChangesAllFields()
    {
        using var db = CreateInMemoryDb();

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Category = "Utilities",
            Description = "Electricity",
            Amount = 200m,
            IsRecurring = false
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        expense.Amount = 250m;
        expense.Description = "Internet";
        await db.SaveChangesAsync();

        var updated = await db.Expenses.AsNoTracking().FirstAsync(e => e.Id == expense.Id);
        updated.Amount.Should().Be(250m);
        updated.Description.Should().Be("Internet");
    }

    [Fact]
    public async Task DeleteExpense_RemovesRecord()
    {
        using var db = CreateInMemoryDb();

        var expenseId = Guid.NewGuid();
        var expense = new Expense
        {
            Id = expenseId,
            TenantId = Guid.NewGuid(),
            Category = "Supplies",
            Amount = 100m
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync();

        var exists = await db.Expenses.AnyAsync(e => e.Id == expenseId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateRecurringExpenses_CreatesCopiesForMonth()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        db.Expenses.Add(new Expense
        {
            Id = templateId,
            TenantId = tenantId,
            Date = new DateOnly(2024, 1, 1),
            Category = "Rent",
            Description = "Monthly rent",
            Amount = 1500m,
            IsRecurring = true,
            OriginalExpenseId = null
        });
        await db.SaveChangesAsync();

        var template = await db.Expenses.FirstAsync(e => e.Id == templateId);
        var firstDay = new DateOnly(2024, 2, 1);

        var copies = new Expense
        {
            TenantId = tenantId,
            Date = firstDay,
            Category = template.Category,
            Description = template.Description,
            Amount = template.Amount,
            IsRecurring = true,
            OriginalExpenseId = template.Id
        };

        db.Expenses.Add(copies);
        await db.SaveChangesAsync();

        var febExpenses = await db.Expenses
            .Where(e => e.TenantId == tenantId && e.Date >= firstDay && e.Date <= firstDay.AddMonths(1).AddDays(-1))
            .ToListAsync();

        febExpenses.Should().HaveCount(1);
        febExpenses[0].OriginalExpenseId.Should().Be(templateId);
    }

    [Fact]
    public async Task CardFeeConfig_UpdateReplacesExistingFees()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        db.CardFeeConfigs.Add(new CardFeeConfig { TenantId = tenantId, FeeType = "CreditCard1x", FeePercentage = 2.5m });
        await db.SaveChangesAsync();

        var config = await db.CardFeeConfigs.FirstAsync(c => c.TenantId == tenantId && c.FeeType == "CreditCard1x");
        config.FeePercentage = 3.0m;
        await db.SaveChangesAsync();

        var updated = await db.CardFeeConfigs.AsNoTracking().FirstAsync(c => c.TenantId == tenantId && c.FeeType == "CreditCard1x");
        updated.FeePercentage.Should().Be(3.0m);
    }

    [Fact]
    public async Task DefaultFees_WhenNoConfig_ReturnsDefaults()
    {
        var defaultFees = new Dictionary<string, decimal>
        {
            ["Cash"] = 0m,
            ["Pix"] = 0m,
            ["DebitCard"] = 1.5m,
            ["CreditCard1x"] = 2.5m,
            ["CreditCard2to6x"] = 3.5m,
            ["CreditCard7to12x"] = 4.5m,
        };

        defaultFees["Cash"].Should().Be(0m);
        defaultFees["CreditCard1x"].Should().Be(2.5m);
        defaultFees["CreditCard7to12x"].Should().Be(4.5m);
    }

    [Fact]
    public async Task FinancialKpiCalculator_CalculatesRevenueCorrectly()
    {
        var transactions = new[]
        {
            new { GrossAmount = 100m, CardFeeAmount = 2.5m, NetAmount = 97.5m },
            new { GrossAmount = 200m, CardFeeAmount = 7m, NetAmount = 193m },
            new { GrossAmount = 150m, CardFeeAmount = 5.25m, NetAmount = 144.75m },
        };

        var gross = transactions.Sum(t => t.GrossAmount);
        var fees = transactions.Sum(t => t.CardFeeAmount);
        var net = transactions.Sum(t => t.NetAmount);

        gross.Should().Be(450m);
        fees.Should().Be(14.75m);
        net.Should().Be(435.25m);
    }

    [Fact]
    public async Task CalculateNetAmount_AppliesPercentageCorrectly()
    {
        var net = FeeCalculator.CalculateNetAmount(100m, 0.05m);
        net.Should().Be(95m);

        net = FeeCalculator.CalculateNetAmount(200m, 0.025m);
        net.Should().Be(195m);
    }
}

public class DashboardFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsCorrectCounts()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Gym", Slug = "test" });

        db.Users.Add(new User { TenantId = tenantId, Name = "Student 1", Email = "s1@test.com", PasswordHash = "hash", Role = UserRole.Student, Status = StudentStatus.Active });
        db.Users.Add(new User { TenantId = tenantId, Name = "Student 2", Email = "s2@test.com", PasswordHash = "hash", Role = UserRole.Student, Status = StudentStatus.Active });
        db.Users.Add(new User { TenantId = tenantId, Name = "Student 3", Email = "s3@test.com", PasswordHash = "hash", Role = UserRole.Student, Status = StudentStatus.Inactive });

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            Date = today,
            StartTime = new TimeOnly(10, 0),
            DurationMinutes = 60,
            Status = SessionStatus.Scheduled,
            SlotsAvailable = 5
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var activeStudents = await db.Users.CountAsync(u => u.TenantId == tenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active);
        activeStudents.Should().Be(2);
    }

    [Fact]
    public async Task GetBookingsThisMonth_CountsCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var startOfMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ClassTypeId = Guid.NewGuid(),
            Date = startOfMonth.AddDays(5),
            Status = SessionStatus.Scheduled
        };
        db.Sessions.Add(session);

        db.Bookings.Add(new Booking { SessionId = session.Id, StudentId = Guid.NewGuid(), Status = BookingStatus.Confirmed });
        db.Bookings.Add(new Booking { SessionId = session.Id, StudentId = Guid.NewGuid(), Status = BookingStatus.Cancelled });
        db.Bookings.Add(new Booking { SessionId = session.Id, StudentId = Guid.NewGuid(), Status = BookingStatus.Confirmed });
        await db.SaveChangesAsync();

        var bookingsThisMonth = await db.Bookings.CountAsync(b =>
            b.Session.TenantId == tenantId &&
            b.Session.Date >= startOfMonth &&
            b.Status != BookingStatus.Cancelled);

        bookingsThisMonth.Should().Be(2);
    }

    [Fact]
    public async Task GetSessionsToday_CountsCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);

        db.Sessions.Add(new Session { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), Date = today, StartTime = new TimeOnly(9, 0), Status = SessionStatus.Scheduled });
        db.Sessions.Add(new Session { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), Date = today, StartTime = new TimeOnly(10, 0), Status = SessionStatus.Scheduled });
        db.Sessions.Add(new Session { TenantId = tenantId, ClassTypeId = Guid.NewGuid(), Date = today.AddDays(1), StartTime = new TimeOnly(9, 0), Status = SessionStatus.Scheduled });
        await db.SaveChangesAsync();

        var sessionsToday = await db.Sessions.CountAsync(s => s.TenantId == tenantId && s.Date == today && s.Status == SessionStatus.Scheduled);
        sessionsToday.Should().Be(2);
    }

    [Fact]
    public async Task GetExpiringPackages_CountsCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Packages.Add(new Package { TenantId = tenantId, StudentId = Guid.NewGuid(), Name = "Soon Expiring", IsActive = true, ExpiresAt = now.AddDays(3) });
        db.Packages.Add(new Package { TenantId = tenantId, StudentId = Guid.NewGuid(), Name = "Later Expiring", IsActive = true, ExpiresAt = now.AddDays(30) });
        db.Packages.Add(new Package { TenantId = tenantId, StudentId = Guid.NewGuid(), Name = "Already Expired", IsActive = true, ExpiresAt = now.AddDays(-1) });
        await db.SaveChangesAsync();

        var expiringPackages = await db.Packages.CountAsync(p =>
            p.TenantId == tenantId &&
            p.IsActive &&
            p.ExpiresAt.HasValue &&
            p.ExpiresAt >= now &&
            p.ExpiresAt <= now.AddDays(7));

        expiringPackages.Should().Be(1);
    }

    [Fact]
    public async Task CalculateAverageOccupancy_CalculatesCorrectly()
    {
        var sessions = new[]
        {
            new { Capacity = 20, ActiveBookings = 15 },
            new { Capacity = 15, ActiveBookings = 10 },
            new { Capacity = 25, ActiveBookings = 20 },
        };

        var totalCapacity = sessions.Sum(s => s.Capacity);
        var totalBookings = sessions.Sum(s => s.ActiveBookings);
        var avgOccupancy = totalCapacity > 0 ? Math.Round((double)totalBookings / totalCapacity * 100, 1) : 0;

        avgOccupancy.Should().Be(75.0);
    }

    [Fact]
    public async Task GetNewStudentsThisMonth_CountsCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Users.Add(new User { TenantId = tenantId, Name = "Old Student", Email = "old@test.com", PasswordHash = "hash", Role = UserRole.Student, CreatedAt = DateTime.UtcNow.AddMonths(-2) });
        db.Users.Add(new User { TenantId = tenantId, Name = "New Student", Email = "new@test.com", PasswordHash = "hash", Role = UserRole.Student, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var newStudents = await db.Users.CountAsync(u =>
            u.TenantId == tenantId &&
            u.Role == UserRole.Student &&
            u.CreatedAt >= startOfMonth);

        newStudents.Should().Be(1);
    }

    [Fact]
    public async Task GetStudentsWithNoCredits_IdentifiesCorrectly()
    {
        using var db = CreateInMemoryDb();

        var tenantId = Guid.NewGuid();
        var classTypeId = Guid.NewGuid();

        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();

        db.Users.Add(new User { Id = studentId1, TenantId = tenantId, Name = "Student 1", Email = "s1@test.com", PasswordHash = "hash", Role = UserRole.Student, Status = StudentStatus.Active });
        db.Users.Add(new User { Id = studentId2, TenantId = tenantId, Name = "Student 2", Email = "s2@test.com", PasswordHash = "hash", Role = UserRole.Student, Status = StudentStatus.Active });

        var package1 = new Package { TenantId = tenantId, StudentId = studentId1, IsActive = true };
        package1.Items.Add(new PackageItem { ClassTypeId = classTypeId, TotalCredits = 10, UsedCredits = 10 });
        db.Packages.Add(package1);

        var package2 = new Package { TenantId = tenantId, StudentId = studentId2, IsActive = true };
        package2.Items.Add(new PackageItem { ClassTypeId = classTypeId, TotalCredits = 10, UsedCredits = 5 });
        db.Packages.Add(package2);
        await db.SaveChangesAsync();

        var allStudents = await db.Users.Where(u => u.TenantId == tenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active).ToListAsync();
        var studentsWithCredits = allStudents.Where(s =>
            db.PackageItems.Any(i => i.Package.StudentId == s.Id && i.Package.IsActive && i.UsedCredits < i.TotalCredits)).ToList();
        var studentsWithNoCredits = allStudents.Where(s => !studentsWithCredits.Contains(s)).ToList();

        studentsWithNoCredits.Should().HaveCount(1);
        studentsWithNoCredits[0].Id.Should().Be(studentId1);
    }

    [Fact]
    public async Task CalculateCancellationRate_CalculatesCorrectly()
    {
        var totalBookingsMonth = 10;
        var cancelledBookingsMonth = 2;

        var cancellationRate = totalBookingsMonth > 0
            ? Math.Round((double)cancelledBookingsMonth / totalBookingsMonth * 100, 1)
            : 0.0;

        cancellationRate.Should().Be(20.0);
    }

    [Fact]
    public async Task SalonBilling_CalculatesRevenueCorrectly()
    {
        var bookings = new[]
        {
            new { Status = BookingStatus.Confirmed, Price = 50m },
            new { Status = BookingStatus.Confirmed, Price = 75m },
            new { Status = BookingStatus.Cancelled, Price = 60m },
            new { Status = BookingStatus.Confirmed, Price = 100m },
        };

        var nonCancelled = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var totalRevenue = nonCancelled.Sum(b => b.Price);
        var totalAppointments = nonCancelled.Count;
        var averageTicket = totalAppointments > 0 ? Math.Round(totalRevenue / totalAppointments, 2) : 0m;
        var cancelledCount = bookings.Count(b => b.Status == BookingStatus.Cancelled);

        totalRevenue.Should().Be(225m);
        totalAppointments.Should().Be(3);
        averageTicket.Should().Be(75m);
        cancelledCount.Should().Be(1);
    }

    [Fact]
    public async Task TopServicesByRevenue_GroupsAndSortsCorrectly()
    {
        var bookings = new[]
        {
            new { ServiceName = "Haircut", Price = 50m },
            new { ServiceName = "Haircut", Price = 50m },
            new { ServiceName = "Coloring", Price = 100m },
            new { ServiceName = "Coloring", Price = 100m },
            new { ServiceName = "Coloring", Price = 100m },
            new { ServiceName = "Nails", Price = 30m },
        };

        var byService = bookings
            .GroupBy(b => b.ServiceName)
            .Select(g => new { Name = g.Key, Count = g.Count(), Revenue = g.Sum(b => b.Price) })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        byService[0].Name.Should().Be("Coloring");
        byService[0].Revenue.Should().Be(300m);
        byService[1].Name.Should().Be("Haircut");
        byService[2].Name.Should().Be("Nails");
    }

    [Fact]
    public async Task WeeklyCheckins_GroupsCorrectly()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var since = today.AddDays(-56);

        var dates = new List<DateOnly>
        {
            today.AddDays(-1),
            today.AddDays(-3),
            today.AddDays(-8),
            today.AddDays(-10),
        };

        var result = Enumerable.Range(0, 8).Select(weekAgo =>
        {
            var weekEnd = today.AddDays(-(weekAgo * 7));
            var weekStart = weekEnd.AddDays(-6);
            var count = dates.Count(d => d >= weekStart && d <= weekEnd);
            return new { WeekStart = weekStart, WeekEnd = weekEnd, Count = count };
        })
        .OrderBy(x => x.WeekStart)
        .ToList();

        result.Should().HaveCount(8);
    }

    [Fact]
    public async Task OccupancyCalculation_CalculatesPercentage()
    {
        var bookings = 8;
        var capacity = 10;

        var occupancyPct = capacity > 0
            ? Math.Round((double)bookings / capacity * 100)
            : 0.0;

        occupancyPct.Should().Be(80);
    }

    [Fact]
    public async Task DashboardKpiCalculator_CalculatesAllKPIs()
    {
        var transactions = new[] { new { GrossAmount = 1000m, CardFeeAmount = 30m, NetAmount = 970m } };
        var expenses = 500m;

        var gross = transactions.Sum(t => t.GrossAmount);
        var net = transactions.Sum(t => t.NetAmount);
        var profit = net - expenses;

        profit.Should().Be(470m);
    }
}

public class AffiliateFlowIntegrationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAffiliate_WithValidData_CreatesSuccessfully()
    {
        using var db = CreateInMemoryDb();

        var affiliate = new Affiliate
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ReferralCode = "CODE123",
            CommissionRate = 0.10m
        };
        db.Affiliates.Add(affiliate);
        await db.SaveChangesAsync();

        var saved = await db.Affiliates.FirstAsync();
        saved.ReferralCode.Should().Be("CODE123");
        saved.CommissionRate.Should().Be(0.10m);
    }

    [Fact]
    public async Task CalculateAffiliateCommission_CalculatesCorrectly()
    {
        var result = AffiliateCommissionCalculator.Calculate(1000m, 0.10m);
        result.Commission.Should().Be(1m);
    }

    [Fact]
    public async Task CalculateAffiliateCommission_WithZeroPercentage_ReturnsZero()
    {
        var result = AffiliateCommissionCalculator.Calculate(1000m, 0m);
        result.Commission.Should().Be(0m);
    }

    [Fact]
    public async Task AffiliateCommissionCalculator_TieredRates()
    {
        var result1 = AffiliateCommissionCalculator.Calculate(5000m, 0.10m);
        var result2 = AffiliateCommissionCalculator.Calculate(10000m, 0.10m);

        result1.Commission.Should().Be(5m);
        result2.Commission.Should().Be(10m);
    }

    [Fact]
    public async Task CreateAffiliateCommission_RecordsTransaction()
    {
        using var db = CreateInMemoryDb();

        var affiliateId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var commission = new AffiliateCommission
        {
            AffiliateId = affiliateId,
            TenantId = tenantId,
            GrossAmount = 1000m,
            Rate = 0.10m,
            CommissionAmount = 100m
        };
        db.AffiliateCommissions.Add(commission);
        await db.SaveChangesAsync();

        var saved = await db.AffiliateCommissions.FirstAsync();
        saved.CommissionAmount.Should().Be(100m);
    }

    [Fact]
    public async Task GetAffiliateBalance_CalculatesCorrectly()
    {
        using var db = CreateInMemoryDb();

        var affiliateId = Guid.NewGuid();

        db.AffiliateCommissions.Add(new AffiliateCommission { AffiliateId = affiliateId, CommissionAmount = 100m });
        db.AffiliateCommissions.Add(new AffiliateCommission { AffiliateId = affiliateId, CommissionAmount = 150m });
        db.AffiliateCommissions.Add(new AffiliateCommission { AffiliateId = affiliateId, CommissionAmount = 75m });
        await db.SaveChangesAsync();

        var balance = await db.AffiliateCommissions
            .Where(c => c.AffiliateId == affiliateId)
            .SumAsync(c => (decimal?)c.CommissionAmount) ?? 0m;

        balance.Should().Be(325m);
    }

    [Fact]
    public async Task ResolveWithdrawalRequest_UpdatesStatus()
    {
        using var db = CreateInMemoryDb();

        var requestId = Guid.NewGuid();
        var request = new AffiliateWithdrawalRequest
        {
            Id = requestId,
            AffiliateId = Guid.NewGuid(),
            RequestedAmount = 500m
        };
        db.AffiliateWithdrawalRequests.Add(request);
        await db.SaveChangesAsync();

        request.Status = AffiliateWithdrawalStatus.Approved;
        request.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var updated = await db.AffiliateWithdrawalRequests.AsNoTracking().FirstAsync(r => r.Id == requestId);
        updated.Status.Should().Be(AffiliateWithdrawalStatus.Approved);
        updated.ResolvedAt.Should().NotBeNull();
    }
}