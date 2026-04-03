using System.Security.Claims;
using GymApp.Api.DTOs;
using GymApp.Domain.Entities;
using GymApp.Domain.Enums;
using GymApp.Domain.Interfaces;
using GymApp.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class AffiliateEndpoints
{
    private const string ConfigMinWithdrawal = "AffiliateMinWithdrawalCents";
    private const string ConfigDefaultRate   = "AffiliateDefaultCommissionRate";

    public static void MapAffiliateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/affiliate").RequireAuthorization("Affiliate");

        // GET /api/affiliate/me
        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db, IConfiguration config) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var affiliate = await db.Affiliates
                .AsNoTracking()
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (affiliate is null) return Results.NotFound();

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";
            var link = $"{baseUrl}/salao.html?ref={affiliate.ReferralCode}";

            return Results.Ok(new AffiliateProfileResponse(
                affiliate.Id,
                affiliate.UserId,
                affiliate.User.Name,
                affiliate.User.Email,
                affiliate.ReferralCode,
                link,
                affiliate.CommissionRate,
                affiliate.CreatedAt
            ));
        });

        // GET /api/affiliate/referrals
        group.MapGet("/referrals", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var affiliate = await db.Affiliates.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliate is null) return Results.NotFound();

            var referrals = await db.AffiliateReferrals
                .AsNoTracking()
                .Where(r => r.AffiliateId == affiliate.Id)
                .Include(r => r.Tenant)
                .ToListAsync();

            var commissionsByTenant = await db.AffiliateCommissions
                .AsNoTracking()
                .Where(c => c.AffiliateId == affiliate.Id)
                .GroupBy(c => c.TenantId)
                .Select(g => new { TenantId = g.Key, Total = g.Sum(c => c.CommissionAmount) })
                .ToListAsync();

            var totalsMap = commissionsByTenant.ToDictionary(x => x.TenantId, x => x.Total);

            var result = referrals.Select(r => new AffiliateReferralResponse(
                r.TenantId,
                r.Tenant.Name,
                r.Tenant.Slug,
                r.Tenant.SubscriptionStatus,
                r.Tenant.IsActive,
                r.RegisteredAt,
                totalsMap.GetValueOrDefault(r.TenantId, 0m)
            )).OrderByDescending(r => r.RegisteredAt).ToList();

            return Results.Ok(result);
        });

        // GET /api/affiliate/commissions?page=1&pageSize=50
        group.MapGet("/commissions", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            int page = 1,
            int pageSize = 50) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var affiliate = await db.Affiliates.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliate is null) return Results.NotFound();

            var query = db.AffiliateCommissions
                .AsNoTracking()
                .Where(c => c.AffiliateId == affiliate.Id)
                .Include(c => c.Tenant)
                .OrderByDescending(c => c.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new AffiliateCommissionResponse(
                    c.Id,
                    c.Tenant.Name,
                    c.SubscriptionPaymentRef,
                    c.GrossAmount,
                    c.Rate,
                    c.CommissionAmount,
                    c.Status,
                    c.CreatedAt
                ))
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET /api/affiliate/balance
        group.MapGet("/balance", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var affiliate = await db.Affiliates.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliate is null) return Results.NotFound();

            var totalEarned = await db.AffiliateCommissions
                .AsNoTracking()
                .Where(c => c.AffiliateId == affiliate.Id)
                .SumAsync(c => c.CommissionAmount);

            var pendingWithdrawal = await db.AffiliateWithdrawalRequests
                .AsNoTracking()
                .Where(w => w.AffiliateId == affiliate.Id
                    && (w.Status == AffiliateWithdrawalStatus.Pending
                        || w.Status == AffiliateWithdrawalStatus.Approved))
                .SumAsync(w => w.RequestedAmount);

            var paidOut = await db.AffiliateCommissions
                .AsNoTracking()
                .Where(c => c.AffiliateId == affiliate.Id && c.Status == AffiliateCommissionStatus.Paid)
                .SumAsync(c => c.CommissionAmount);

            var available = totalEarned - paidOut - pendingWithdrawal;

            var minCents = await GetConfigInt(db, ConfigMinWithdrawal, 1000);

            return Results.Ok(new AffiliateBalanceResponse(
                Math.Max(0m, available),
                totalEarned,
                pendingWithdrawal,
                minCents
            ));
        });

        // POST /api/affiliate/withdrawal
        group.MapPost("/withdrawal", async (
            CreateWithdrawalRequest req,
            ClaimsPrincipal principal,
            AppDbContext db,
            IEmailService emailService,
            IConfiguration config) =>
        {
            if (req.Amount <= 0)
                return Results.BadRequest("Valor deve ser maior que zero.");

            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var affiliate = await db.Affiliates
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliate is null) return Results.NotFound();

            var minCents = await GetConfigInt(db, ConfigMinWithdrawal, 1000);
            var minAmount = minCents / 100m;

            if (req.Amount < minAmount)
                return Results.BadRequest($"Valor mínimo para saque é R${minAmount:F2}.");

            // Check available balance
            var totalEarned = await db.AffiliateCommissions
                .Where(c => c.AffiliateId == affiliate.Id)
                .SumAsync(c => c.CommissionAmount);

            var pendingWithdrawal = await db.AffiliateWithdrawalRequests
                .Where(w => w.AffiliateId == affiliate.Id
                    && (w.Status == AffiliateWithdrawalStatus.Pending
                        || w.Status == AffiliateWithdrawalStatus.Approved))
                .SumAsync(w => w.RequestedAmount);

            var paidOut = await db.AffiliateCommissions
                .Where(c => c.AffiliateId == affiliate.Id && c.Status == AffiliateCommissionStatus.Paid)
                .SumAsync(c => c.CommissionAmount);

            var available = totalEarned - paidOut - pendingWithdrawal;

            if (req.Amount > available)
                return Results.BadRequest("Saldo disponível insuficiente.");

            var withdrawal = new AffiliateWithdrawalRequest
            {
                AffiliateId = affiliate.Id,
                RequestedAmount = req.Amount,
                Status = AffiliateWithdrawalStatus.Pending
            };
            db.AffiliateWithdrawalRequests.Add(withdrawal);
            await db.SaveChangesAsync();

            return Results.Created($"/api/affiliate/withdrawals/{withdrawal.Id}", new AffiliateWithdrawalResponse(
                withdrawal.Id,
                withdrawal.RequestedAmount,
                withdrawal.Status,
                withdrawal.AdminNotes,
                withdrawal.CreatedAt,
                withdrawal.ResolvedAt
            ));
        });

        // GET /api/affiliate/withdrawals
        group.MapGet("/withdrawals", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var affiliate = await db.Affiliates.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId);
            if (affiliate is null) return Results.NotFound();

            var withdrawals = await db.AffiliateWithdrawalRequests
                .AsNoTracking()
                .Where(w => w.AffiliateId == affiliate.Id)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new AffiliateWithdrawalResponse(
                    w.Id,
                    w.RequestedAmount,
                    w.Status,
                    w.AdminNotes,
                    w.CreatedAt,
                    w.ResolvedAt
                ))
                .ToListAsync();

            return Results.Ok(withdrawals);
        });
    }

    // ── Super admin: affiliate management ────────────────────────────────────

    public static void MapSuperAdminAffiliateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/affiliates").RequireAuthorization("SuperAdmin");

        // GET /api/admin/affiliates
        group.MapGet("/", async (AppDbContext db, IConfiguration config) =>
        {
            var affiliates = await db.Affiliates
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Referrals)
                .Include(a => a.Commissions)
                .Include(a => a.WithdrawalRequests)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";

            var result = affiliates.Select(a =>
            {
                var totalEarned = a.Commissions.Sum(c => c.CommissionAmount);
                var paidOut     = a.Commissions.Where(c => c.Status == AffiliateCommissionStatus.Paid).Sum(c => c.CommissionAmount);
                var pending     = a.WithdrawalRequests
                    .Where(w => w.Status == AffiliateWithdrawalStatus.Pending || w.Status == AffiliateWithdrawalStatus.Approved)
                    .Sum(w => w.RequestedAmount);
                var available   = Math.Max(0m, totalEarned - paidOut - pending);

                return new AffiliateListResponse(
                    a.Id, a.UserId, a.User.Name, a.User.Email,
                    a.ReferralCode, a.CommissionRate,
                    a.Referrals.Count, totalEarned, available,
                    a.CreatedAt
                );
            }).ToList();

            return Results.Ok(result);
        });

        // POST /api/admin/affiliates
        group.MapPost("/", async (CreateAffiliateRequest req, AppDbContext db, IConfiguration config) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email)
                || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.ReferralCode))
                return Results.BadRequest("Nome, email, senha e código são obrigatórios.");

            if (req.CommissionRate is < 0 or > 1)
                return Results.BadRequest("Taxa deve estar entre 0 e 1.");

            var email = req.Email.ToLowerInvariant();
            var code  = req.ReferralCode.ToLowerInvariant().Trim();

            if (await db.Users.AnyAsync(u => u.Email == email))
                return Results.Conflict("Email já cadastrado.");

            if (await db.Affiliates.AnyAsync(a => a.ReferralCode == code))
                return Results.Conflict("Código de afiliado já em uso.");

            var user = new GymApp.Domain.Entities.User
            {
                Name         = req.Name.Trim(),
                Email        = email,
                Role         = GymApp.Domain.Enums.UserRole.Affiliate,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                TenantId     = null
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var affiliate = new Affiliate
            {
                UserId         = user.Id,
                ReferralCode   = code,
                CommissionRate = req.CommissionRate
            };
            db.Affiliates.Add(affiliate);
            await db.SaveChangesAsync();

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";
            var link    = $"{baseUrl}/salao.html?ref={code}";

            return Results.Created($"/api/admin/affiliates/{affiliate.Id}",
                new AffiliateProfileResponse(
                    affiliate.Id, user.Id, user.Name, user.Email,
                    code, link, affiliate.CommissionRate, affiliate.CreatedAt
                ));
        });

        // GET /api/admin/affiliates/{id}
        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, IConfiguration config) =>
        {
            var affiliate = await db.Affiliates
                .AsNoTracking()
                .Include(a => a.User)
                .Include(a => a.Referrals).ThenInclude(r => r.Tenant)
                .Include(a => a.Commissions).ThenInclude(c => c.Tenant)
                .Include(a => a.WithdrawalRequests)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (affiliate is null) return Results.NotFound();

            var baseUrl = config["App:BaseUrl"]?.TrimEnd('/') ?? "https://agendofy.com";
            var link    = $"{baseUrl}/salao.html?ref={affiliate.ReferralCode}";

            var totalEarned = affiliate.Commissions.Sum(c => c.CommissionAmount);
            var paidOut     = affiliate.Commissions.Where(c => c.Status == AffiliateCommissionStatus.Paid).Sum(c => c.CommissionAmount);
            var pending     = affiliate.WithdrawalRequests
                .Where(w => w.Status == AffiliateWithdrawalStatus.Pending || w.Status == AffiliateWithdrawalStatus.Approved)
                .Sum(w => w.RequestedAmount);
            var available   = Math.Max(0m, totalEarned - paidOut - pending);

            var commissionsByTenant = affiliate.Commissions
                .GroupBy(c => c.TenantId)
                .ToDictionary(g => g.Key, g => g.Sum(c => c.CommissionAmount));

            var referrals = affiliate.Referrals
                .OrderByDescending(r => r.RegisteredAt)
                .Select(r => new AffiliateReferralResponse(
                    r.TenantId, r.Tenant.Name, r.Tenant.Slug,
                    r.Tenant.SubscriptionStatus, r.Tenant.IsActive,
                    r.RegisteredAt,
                    commissionsByTenant.GetValueOrDefault(r.TenantId, 0m)
                )).ToList();

            var commissions = affiliate.Commissions
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new AffiliateCommissionResponse(
                    c.Id, c.Tenant.Name, c.SubscriptionPaymentRef,
                    c.GrossAmount, c.Rate, c.CommissionAmount, c.Status, c.CreatedAt
                )).ToList();

            var withdrawals = affiliate.WithdrawalRequests
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new AffiliateWithdrawalResponse(
                    w.Id, w.RequestedAmount, w.Status, w.AdminNotes, w.CreatedAt, w.ResolvedAt
                )).ToList();

            return Results.Ok(new AffiliateDetailResponse(
                affiliate.Id, affiliate.UserId, affiliate.User.Name, affiliate.User.Email,
                affiliate.ReferralCode, link, affiliate.CommissionRate,
                totalEarned, available, affiliate.CreatedAt,
                referrals, commissions, withdrawals
            ));
        });

        // PATCH /api/admin/affiliates/{id}/rate
        group.MapPatch("/{id:guid}/rate", async (Guid id, UpdateAffiliateRateRequest req, AppDbContext db) =>
        {
            if (req.CommissionRate is < 0 or > 1)
                return Results.BadRequest("Taxa deve estar entre 0 e 1.");

            var affiliate = await db.Affiliates.FindAsync(id);
            if (affiliate is null) return Results.NotFound();

            affiliate.CommissionRate = req.CommissionRate;
            await db.SaveChangesAsync();

            return Results.Ok(new { affiliateId = id, commissionRate = affiliate.CommissionRate });
        });

        // GET /api/admin/affiliates/withdrawals
        group.MapGet("/withdrawals", async (AppDbContext db, string? status = null) =>
        {
            var query = db.AffiliateWithdrawalRequests
                .AsNoTracking()
                .Include(w => w.Affiliate).ThenInclude(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status)
                && Enum.TryParse<AffiliateWithdrawalStatus>(status, true, out var parsed))
                query = query.Where(w => w.Status == parsed);

            var withdrawals = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new AdminWithdrawalResponse(
                    w.Id,
                    w.AffiliateId,
                    w.Affiliate.User.Name,
                    w.Affiliate.User.Email,
                    w.RequestedAmount,
                    w.Status,
                    w.AdminNotes,
                    w.CreatedAt,
                    w.ResolvedAt
                ))
                .ToListAsync();

            return Results.Ok(withdrawals);
        });

        // PATCH /api/admin/affiliates/withdrawals/{id}
        group.MapPatch("/withdrawals/{id:guid}", async (
            Guid id,
            ResolveWithdrawalRequest req,
            AppDbContext db,
            IEmailService emailService) =>
        {
            if (req.Status != AffiliateWithdrawalStatus.Approved
                && req.Status != AffiliateWithdrawalStatus.Rejected)
                return Results.BadRequest("Status deve ser Approved ou Rejected.");

            var withdrawal = await db.AffiliateWithdrawalRequests
                .Include(w => w.Affiliate).ThenInclude(a => a.User)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (withdrawal is null) return Results.NotFound();
            if (withdrawal.Status != AffiliateWithdrawalStatus.Pending)
                return Results.Conflict("Solicitação já foi resolvida.");

            withdrawal.Status      = req.Status;
            withdrawal.AdminNotes  = req.AdminNotes;
            withdrawal.ResolvedAt  = DateTime.UtcNow;

            // If approved, mark corresponding commissions as Paid
            if (req.Status == AffiliateWithdrawalStatus.Approved)
            {
                var pendingCommissions = await db.AffiliateCommissions
                    .Where(c => c.AffiliateId == withdrawal.AffiliateId
                             && c.Status == AffiliateCommissionStatus.Pending)
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync();

                var remaining = withdrawal.RequestedAmount;
                foreach (var commission in pendingCommissions)
                {
                    if (remaining <= 0) break;
                    commission.Status = AffiliateCommissionStatus.Paid;
                    remaining -= commission.CommissionAmount;
                }
            }

            await db.SaveChangesAsync();

            // Send email notification
            var statusText = req.Status == AffiliateWithdrawalStatus.Approved ? "aprovada" : "rejeitada";
            await emailService.SendAffiliateWithdrawalStatusAsync(
                withdrawal.Affiliate.User.Email,
                withdrawal.Affiliate.User.Name,
                withdrawal.RequestedAmount,
                statusText,
                withdrawal.AdminNotes
            );

            return Results.Ok(new AffiliateWithdrawalResponse(
                withdrawal.Id, withdrawal.RequestedAmount, withdrawal.Status,
                withdrawal.AdminNotes, withdrawal.CreatedAt, withdrawal.ResolvedAt
            ));
        });
    }

    // ── System config ─────────────────────────────────────────────────────────

    public static void MapSystemConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/config").RequireAuthorization("SuperAdmin");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var minCents = await GetConfigInt(db, ConfigMinWithdrawal, 1000);
            var rate     = await GetConfigDecimal(db, ConfigDefaultRate, 0.20m);
            return Results.Ok(new SystemConfigResponse(minCents, rate));
        });

        group.MapPatch("/", async (UpdateSystemConfigRequest req, AppDbContext db) =>
        {
            if (req.AffiliateMinWithdrawalCents.HasValue)
            {
                if (req.AffiliateMinWithdrawalCents.Value < 0)
                    return Results.BadRequest("Valor mínimo não pode ser negativo.");
                await SetConfig(db, ConfigMinWithdrawal, req.AffiliateMinWithdrawalCents.Value.ToString());
            }

            if (req.AffiliateDefaultCommissionRate.HasValue)
            {
                if (req.AffiliateDefaultCommissionRate.Value is < 0 or > 1)
                    return Results.BadRequest("Taxa deve estar entre 0 e 1.");
                await SetConfig(db, ConfigDefaultRate, req.AffiliateDefaultCommissionRate.Value.ToString());
            }

            var minCents = await GetConfigInt(db, ConfigMinWithdrawal, 1000);
            var rate     = await GetConfigDecimal(db, ConfigDefaultRate, 0.20m);
            return Results.Ok(new SystemConfigResponse(minCents, rate));
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static async Task<int> GetConfigInt(AppDbContext db, string key, int defaultValue)
    {
        var cfg = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Key == key);
        return cfg is not null && int.TryParse(cfg.Value, out var v) ? v : defaultValue;
    }

    internal static async Task<decimal> GetConfigDecimal(AppDbContext db, string key, decimal defaultValue)
    {
        var cfg = await db.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Key == key);
        return cfg is not null && decimal.TryParse(cfg.Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    private static async Task SetConfig(AppDbContext db, string key, string value)
    {
        var cfg = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
        if (cfg is null)
        {
            db.SystemConfigs.Add(new SystemConfig { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            cfg.Value     = value;
            cfg.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}
