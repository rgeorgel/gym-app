using System.Security.Claims;
using GymApp.Api.Services;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Endpoints;

public static class AiAssistantEndpoints
{
    public static void MapAiAssistantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/ai").RequireAuthorization("AdminOrAbove");

        // POST /admin/ai/chat — send a message and get a response
        group.MapPost("/chat", async (
            ChatRequest req,
            AppDbContext db,
            TenantContext tenant,
            ClaimsPrincipal principal,
            AiAssistantService ai) =>
        {
            var adminId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tenantData = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
            var tenantName = tenantData?.Name ?? "seu negócio";

            if (string.IsNullOrWhiteSpace(req.Message))
                return Results.BadRequest("Mensagem não pode ser vazia.");

            var response = await ai.ChatAsync(db, tenant, adminId, req.Message, req.ConversationId, tenantName);
            return Results.Ok(response);
        });

        // GET /admin/ai/conversations — list conversations for the current admin
        group.MapGet("/conversations", async (
            AppDbContext db,
            TenantContext tenant,
            ClaimsPrincipal principal) =>
        {
            var adminId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var conversations = await db.AiConversations.AsNoTracking()
                .Where(c => c.TenantId == tenant.TenantId && c.AdminUserId == adminId)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new { c.Id, c.Title, c.CreatedAt, c.UpdatedAt })
                .Take(50)
                .ToListAsync();

            return Results.Ok(conversations);
        });

        // GET /admin/ai/conversations/{id} — get messages of a conversation
        group.MapGet("/conversations/{id:guid}", async (
            Guid id,
            AppDbContext db,
            TenantContext tenant,
            ClaimsPrincipal principal) =>
        {
            var adminId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var conversation = await db.AiConversations.AsNoTracking()
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId && c.AdminUserId == adminId);

            if (conversation is null) return Results.NotFound();

            var messages = conversation.Messages
                .Where(m => m.Role == "user" || m.Role == "assistant")
                .Select(m => new { m.Id, m.Role, m.Content, m.CreatedAt });

            return Results.Ok(new { conversation.Id, conversation.Title, Messages = messages });
        });

        // PUT /api/admin/settings/ai-prompt — update the AI system prompt for this tenant
        app.MapPut("/api/admin/settings/ai-prompt", async (
            AiPromptRequest req,
            AppDbContext db,
            TenantContext tenant) =>
        {
            var tenantData = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
            if (tenantData is null) return Results.NotFound();

            tenantData.AiSystemPrompt = string.IsNullOrWhiteSpace(req.SystemPrompt) ? null : req.SystemPrompt.Trim();
            await db.SaveChangesAsync();

            return Results.Ok(new { tenantData.AiSystemPrompt });
        }).RequireAuthorization("AdminOrAbove");

        // GET /api/admin/settings/ai-prompt — get current prompt
        app.MapGet("/api/admin/settings/ai-prompt", async (
            AppDbContext db,
            TenantContext tenant) =>
        {
            var tenantData = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
            return Results.Ok(new { AiSystemPrompt = tenantData?.AiSystemPrompt });
        }).RequireAuthorization("AdminOrAbove");
    }
}

public record ChatRequest(string Message, Guid? ConversationId);
public record AiPromptRequest(string? SystemPrompt);
