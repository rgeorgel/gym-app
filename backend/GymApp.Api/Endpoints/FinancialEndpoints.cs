using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class FinancialEndpoints
{
    private static readonly Dictionary<string, decimal> DefaultFees = new()
    {
        ["Cash"]            = 0m,
        ["Pix"]             = 0m,
        ["DebitCard"]       = 1.5m,
        ["CreditCard1x"]    = 2.5m,
        ["CreditCard2to6x"] = 3.5m,
        ["CreditCard7to12x"]= 4.5m,
    };

    public static void MapFinancialEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/financial").RequireAuthorization("AdminOrAbove");

        // ── Card fee config ────────────────────────────────────────────────────
        group.MapGet("/fees", async (AppDbContext db, TenantContext tenant) =>
        {
            var configs = await db.CardFeeConfigs.AsNoTracking()
                .Where(c => c.TenantId == tenant.TenantId)
                .ToListAsync();

            if (!configs.Any())
            {
                configs = DefaultFees.Select(kv => new CardFeeConfig
                {
                    TenantId = tenant.TenantId,
                    FeeType = kv.Key,
                    FeePercentage = kv.Value,
                }).ToList();
                db.CardFeeConfigs.AddRange(configs);
                await db.SaveChangesAsync();
            }

            return Results.Ok(configs.Select(c => new CardFeeConfigResponse(c.FeeType, c.FeePercentage)));
        });

        group.MapPut("/fees", async (UpdateCardFeesRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var updates = new Dictionary<string, decimal>
            {
                ["Cash"]             = req.Cash,
                ["Pix"]              = req.Pix,
                ["DebitCard"]        = req.DebitCard,
                ["CreditCard1x"]     = req.CreditCard1x,
                ["CreditCard2to6x"]  = req.CreditCard2to6x,
                ["CreditCard7to12x"] = req.CreditCard7to12x,
            };

            var existing = await db.CardFeeConfigs
                .Where(c => c.TenantId == tenant.TenantId)
                .ToListAsync();

            foreach (var (feeType, pct) in updates)
            {
                var cfg = existing.FirstOrDefault(c => c.FeeType == feeType);
                if (cfg is null)
                {
                    db.CardFeeConfigs.Add(new CardFeeConfig { TenantId = tenant.TenantId, FeeType = feeType, FeePercentage = pct });
                }
                else
                {
                    cfg.FeePercentage = pct;
                }
            }
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // ── Transactions (revenue) ─────────────────────────────────────────────
        group.MapGet("/transactions", async (AppDbContext db, TenantContext tenant,
            DateOnly? from, DateOnly? to, Guid? studentId) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var end   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var query = db.FinancialTransactions.AsNoTracking()
                .Where(t => t.TenantId == tenant.TenantId && t.Date >= start && t.Date <= end);

            if (studentId.HasValue)
                query = query.Where(t => t.StudentId == studentId.Value);

            var items = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
                .Select(t => new TransactionResponse(t.Id, t.Date, t.StudentId, t.StudentName,
                    t.BookingId, t.ServiceName, t.GrossAmount, t.PaymentMethod.ToString(),
                    t.Installments, t.CardFeePercentage, t.CardFeeAmount, t.NetAmount, t.Notes, t.CreatedAt))
                .ToListAsync();

            return Results.Ok(items);
        });

        group.MapPost("/transactions", async (CreateTransactionRequest req, AppDbContext db, TenantContext tenant) =>
        {
            if (req.GrossAmount <= 0) return Results.BadRequest("GrossAmount must be positive.");
            if (!Enum.TryParse<PaymentMethod>(req.PaymentMethod, out var pm))
                return Results.BadRequest("Invalid PaymentMethod.");

            var feeType = ResolveFeeType(pm, req.Installments);
            var feeCfg = await db.CardFeeConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId && c.FeeType == feeType);
            var feePct = feeCfg?.FeePercentage ?? DefaultFees.GetValueOrDefault(feeType, 0m);
            var feeAmt = Math.Round(req.GrossAmount * feePct / 100m, 2);
            var netAmt = req.GrossAmount - feeAmt;

            // Resolve student name if studentId provided
            string? studentName = req.StudentName;
            if (req.StudentId.HasValue && string.IsNullOrWhiteSpace(studentName))
            {
                var student = await db.Users.AsNoTracking()
                    .Where(u => u.Id == req.StudentId.Value && u.TenantId == tenant.TenantId)
                    .Select(u => u.Name)
                    .FirstOrDefaultAsync();
                studentName = student;
            }

            var tx = new FinancialTransaction
            {
                TenantId           = tenant.TenantId,
                Date               = req.Date,
                StudentId          = req.StudentId,
                StudentName        = studentName,
                BookingId          = req.BookingId,
                ServiceName        = req.ServiceName.Trim(),
                GrossAmount        = req.GrossAmount,
                PaymentMethod      = pm,
                Installments       = Math.Max(1, req.Installments),
                CardFeePercentage  = feePct,
                CardFeeAmount      = feeAmt,
                NetAmount          = netAmt,
                Notes              = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            };
            db.FinancialTransactions.Add(tx);
            await db.SaveChangesAsync();

            return Results.Created($"/api/financial/transactions/{tx.Id}",
                new TransactionResponse(tx.Id, tx.Date, tx.StudentId, tx.StudentName, tx.BookingId,
                    tx.ServiceName, tx.GrossAmount, tx.PaymentMethod.ToString(), tx.Installments,
                    tx.CardFeePercentage, tx.CardFeeAmount, tx.NetAmount, tx.Notes, tx.CreatedAt));
        });

        group.MapPut("/transactions/{id:guid}", async (Guid id, UpdateTransactionRequest req, AppDbContext db, TenantContext tenant) =>
        {
            if (req.GrossAmount <= 0) return Results.BadRequest("GrossAmount must be positive.");
            if (!Enum.TryParse<PaymentMethod>(req.PaymentMethod, out var pm))
                return Results.BadRequest("Invalid PaymentMethod.");

            var tx = await db.FinancialTransactions
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.TenantId);
            if (tx is null) return Results.NotFound();

            var feeType = ResolveFeeType(pm, req.Installments);
            var feeCfg = await db.CardFeeConfigs.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId && c.FeeType == feeType);
            var feePct = feeCfg?.FeePercentage ?? DefaultFees.GetValueOrDefault(feeType, 0m);
            var feeAmt = Math.Round(req.GrossAmount * feePct / 100m, 2);

            tx.Date               = req.Date;
            tx.ServiceName        = req.ServiceName.Trim();
            tx.GrossAmount        = req.GrossAmount;
            tx.PaymentMethod      = pm;
            tx.Installments       = Math.Max(1, req.Installments);
            tx.CardFeePercentage  = feePct;
            tx.CardFeeAmount      = feeAmt;
            tx.NetAmount          = req.GrossAmount - feeAmt;
            tx.StudentName        = string.IsNullOrWhiteSpace(req.StudentName) ? tx.StudentName : req.StudentName.Trim();
            tx.Notes              = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
            await db.SaveChangesAsync();

            return Results.Ok(new TransactionResponse(tx.Id, tx.Date, tx.StudentId, tx.StudentName, tx.BookingId,
                tx.ServiceName, tx.GrossAmount, tx.PaymentMethod.ToString(), tx.Installments,
                tx.CardFeePercentage, tx.CardFeeAmount, tx.NetAmount, tx.Notes, tx.CreatedAt));
        });

        group.MapDelete("/transactions/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var tx = await db.FinancialTransactions
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.TenantId);
            if (tx is null) return Results.NotFound();
            db.FinancialTransactions.Remove(tx);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── Expenses ───────────────────────────────────────────────────────────
        group.MapGet("/expenses", async (AppDbContext db, TenantContext tenant, int? year, int? month) =>
        {
            var now   = DateTime.UtcNow;
            var y     = year  ?? now.Year;
            var m     = month ?? now.Month;
            var start = new DateOnly(y, m, 1);
            var end   = start.AddMonths(1).AddDays(-1);

            // Auto-generate recurring expenses for this month if not yet generated
            await GenerateRecurringExpenses(db, tenant.TenantId, y, m);

            var items = await db.Expenses.AsNoTracking()
                .Where(e => e.TenantId == tenant.TenantId && e.Date >= start && e.Date <= end)
                .OrderBy(e => e.Date).ThenBy(e => e.Category)
                .Select(e => new ExpenseResponse(e.Id, e.Date, e.Category, e.Description,
                    e.Amount, e.IsRecurring, e.OriginalExpenseId, e.CreatedAt))
                .ToListAsync();

            return Results.Ok(items);
        });

        group.MapPost("/expenses", async (CreateExpenseRequest req, AppDbContext db, TenantContext tenant) =>
        {
            if (req.Amount <= 0) return Results.BadRequest("Amount must be positive.");

            var expense = new Expense
            {
                TenantId    = tenant.TenantId,
                Date        = req.Date,
                Category    = req.Category.Trim(),
                Description = req.Description.Trim(),
                Amount      = req.Amount,
                IsRecurring = req.IsRecurring,
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();

            return Results.Created($"/api/financial/expenses/{expense.Id}",
                new ExpenseResponse(expense.Id, expense.Date, expense.Category, expense.Description,
                    expense.Amount, expense.IsRecurring, expense.OriginalExpenseId, expense.CreatedAt));
        });

        group.MapPut("/expenses/{id:guid}", async (Guid id, UpdateExpenseRequest req, AppDbContext db, TenantContext tenant) =>
        {
            var expense = await db.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenant.TenantId);
            if (expense is null) return Results.NotFound();
            if (req.Amount <= 0) return Results.BadRequest("Amount must be positive.");

            expense.Date        = req.Date;
            expense.Category    = req.Category.Trim();
            expense.Description = req.Description.Trim();
            expense.Amount      = req.Amount;
            expense.IsRecurring = req.IsRecurring;
            await db.SaveChangesAsync();

            return Results.Ok(new ExpenseResponse(expense.Id, expense.Date, expense.Category,
                expense.Description, expense.Amount, expense.IsRecurring, expense.OriginalExpenseId, expense.CreatedAt));
        });

        group.MapDelete("/expenses/{id:guid}", async (Guid id, AppDbContext db, TenantContext tenant) =>
        {
            var expense = await db.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenant.TenantId);
            if (expense is null) return Results.NotFound();
            db.Expenses.Remove(expense);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── Dashboard ─────────────────────────────────────────────────────────
        group.MapGet("/dashboard", async (AppDbContext db, TenantContext tenant, int? year, int? month) =>
        {
            var now = DateTime.UtcNow;
            var y   = year  ?? now.Year;
            var m   = month ?? now.Month;

            var (gross, fees, net, count) = await GetRevenueKpis(db, tenant.TenantId, y, m);
            var expenses = await GetExpensesTotal(db, tenant.TenantId, y, m);
            var profit = net - expenses;
            var ticket = count > 0 ? Math.Round(gross / count, 2) : 0m;

            var prevM = m == 1 ? 12 : m - 1;
            var prevY = m == 1 ? y - 1 : y;
            var (prevGross, _, prevNet, _) = await GetRevenueKpis(db, tenant.TenantId, prevY, prevM);
            var prevExpenses = await GetExpensesTotal(db, tenant.TenantId, prevY, prevM);
            var prevProfit = prevNet - prevExpenses;

            return Results.Ok(new FinancialDashboardResponse(
                gross, fees, net, expenses, profit, ticket, count,
                prevGross, prevNet, prevExpenses, prevProfit));
        });
    }

    private static async Task<(decimal gross, decimal fees, decimal net, int count)>
        GetRevenueKpis(AppDbContext db, Guid tenantId, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end   = start.AddMonths(1).AddDays(-1);
        var rows  = await db.FinancialTransactions.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Date >= start && t.Date <= end)
            .Select(t => new { t.GrossAmount, t.CardFeeAmount, t.NetAmount })
            .ToListAsync();
        return (rows.Sum(r => r.GrossAmount), rows.Sum(r => r.CardFeeAmount),
                rows.Sum(r => r.NetAmount), rows.Count);
    }

    private static async Task<decimal> GetExpensesTotal(AppDbContext db, Guid tenantId, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end   = start.AddMonths(1).AddDays(-1);
        return await db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Date >= start && e.Date <= end)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;
    }

    private static async Task GenerateRecurringExpenses(AppDbContext db, Guid tenantId, int year, int month)
    {
        var firstDay = new DateOnly(year, month, 1);

        // Root recurring templates: IsRecurring=true, OriginalExpenseId=null, date before this month
        var templates = await db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsRecurring && e.OriginalExpenseId == null
                        && e.Date < firstDay)
            .ToListAsync();

        if (!templates.Any()) return;

        // Check which templates already have a copy this month
        var templateIds = templates.Select(t => t.Id).ToList();
        var existing = await db.Expenses.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.OriginalExpenseId != null
                        && templateIds.Contains(e.OriginalExpenseId.Value)
                        && e.Date >= firstDay && e.Date <= firstDay.AddMonths(1).AddDays(-1))
            .Select(e => e.OriginalExpenseId!.Value)
            .ToListAsync();

        var toCreate = templates.Where(t => !existing.Contains(t.Id)).ToList();
        if (!toCreate.Any()) return;

        var copies = toCreate.Select(t => new Expense
        {
            TenantId           = tenantId,
            Date               = firstDay,
            Category           = t.Category,
            Description        = t.Description,
            Amount             = t.Amount,
            IsRecurring        = true,
            OriginalExpenseId  = t.Id,
        }).ToList();

        db.Expenses.AddRange(copies);
        await db.SaveChangesAsync();
    }

    private static string ResolveFeeType(PaymentMethod pm, int installments) => pm switch
    {
        PaymentMethod.Cash      => "Cash",
        PaymentMethod.Pix       => "Pix",
        PaymentMethod.DebitCard => "DebitCard",
        PaymentMethod.CreditCard when installments <= 1  => "CreditCard1x",
        PaymentMethod.CreditCard when installments <= 6  => "CreditCard2to6x",
        PaymentMethod.CreditCard                         => "CreditCard7to12x",
        _ => "Cash"
    };
}
