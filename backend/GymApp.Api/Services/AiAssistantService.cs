using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GymApp.Domain.Enums;
using GymApp.Infra.Data;
using GymApp.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace GymApp.Api.Services;

public class AiAssistantService(IConfiguration config, IHttpClientFactory httpFactory, IHttpContextAccessor http)
{
    private readonly IHttpContextAccessor _http = http;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object[] _tools =
    [
        new
        {
            type = "function",
            function = new
            {
                name = "list_students",
                description = "Lista os clientes/alunos do negócio. Pode filtrar por nome, email ou status.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        search = new { type = "string", description = "Texto para filtrar por nome ou email" },
                        status = new { type = "string", @enum = new[] { "Active", "Inactive" }, description = "Filtrar por status" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_student",
                description = "Retorna detalhes completos de um cliente/aluno pelo ID.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        student_id = new { type = "string", description = "UUID do aluno/cliente" }
                    },
                    required = new[] { "student_id" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_sessions",
                description = "Lista as sessões/aulas agendadas em um intervalo de datas.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        from = new { type = "string", description = "Data inicial no formato YYYY-MM-DD (padrão: hoje)" },
                        to = new { type = "string", description = "Data final no formato YYYY-MM-DD (padrão: +13 dias)" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_dashboard_stats",
                description = "Retorna estatísticas gerais do negócio: total de clientes, agendamentos, receita, ocupação média.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_expiring_packages",
                description = "Lista pacotes que estão prestes a vencer nos próximos 14 dias.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_inactive_students",
                description = "Lista clientes/alunos que não comparecem há N dias.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        days = new { type = "integer", description = "Número de dias de inatividade (padrão: 14)" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_service_categories",
                description = "Lista as categorias de serviços cadastradas.",
                parameters = new { type = "object", properties = new { } }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_service_category",
                description = "Cadastra uma nova categoria de serviços.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Nome da categoria" },
                        sort_order = new { type = "integer", description = "Ordem de exibição (padrão: 0)" }
                    },
                    required = new[] { "name" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "update_service_category",
                description = "Atualiza uma categoria de serviços existente.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        category_id = new { type = "string", description = "UUID da categoria" },
                        name = new { type = "string", description = "Novo nome" },
                        sort_order = new { type = "integer", description = "Nova ordem de exibição" },
                        is_active = new { type = "boolean", description = "true para ativa, false para inativa" }
                    },
                    required = new[] { "category_id", "name" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_services",
                description = "Lista os serviços/modalidades cadastrados no negócio (ativos e inativos).",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_service",
                description = "Cadastra um novo serviço/modalidade. Use modality_type='Individual' para salões (atendimento individual).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new { type = "string", description = "Nome do serviço" },
                        description = new { type = "string", description = "Descrição opcional" },
                        price = new { type = "number", description = "Preço em reais" },
                        duration_minutes = new { type = "integer", description = "Duração em minutos" },
                        color = new { type = "string", description = "Cor em hex, ex: #e94560 (opcional, padrão #6c757d)" },
                        modality_type = new { type = "string", @enum = new[] { "Individual", "Group", "Pair" }, description = "Tipo: Individual (salão), Group (academia), Pair (dupla)" },
                        category_name = new { type = "string", description = "Nome da categoria à qual o serviço pertence (opcional). Use list_service_categories para ver as disponíveis." }
                    },
                    required = new[] { "name", "modality_type" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "update_service",
                description = "Atualiza um serviço/modalidade existente pelo ID.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        service_id = new { type = "string", description = "UUID do serviço" },
                        name = new { type = "string", description = "Novo nome" },
                        description = new { type = "string", description = "Nova descrição" },
                        price = new { type = "number", description = "Novo preço em reais" },
                        duration_minutes = new { type = "integer", description = "Nova duração em minutos" },
                        color = new { type = "string", description = "Nova cor em hex" },
                        is_active = new { type = "boolean", description = "true para ativo, false para inativo" },
                        modality_type = new { type = "string", @enum = new[] { "Individual", "Group", "Pair" } },
                        category_name = new { type = "string", description = "Nome da categoria à qual o serviço pertence. Envie string vazia para remover a categoria." }
                    },
                    required = new[] { "service_id", "name", "modality_type" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "list_availability",
                description = "Lista os blocos de disponibilidade/horários de atendimento configurados.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_availability",
                description = "Cadastra um bloco de disponibilidade (horário de atendimento). Weekday: 0=Dom, 1=Seg, 2=Ter, 3=Qua, 4=Qui, 5=Sex, 6=Sáb.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        weekday = new { type = "integer", description = "Dia da semana (0=Dom até 6=Sáb)" },
                        start_time = new { type = "string", description = "Horário de início no formato HH:MM, ex: 09:00" },
                        end_time = new { type = "string", description = "Horário de fim no formato HH:MM, ex: 18:00" }
                    },
                    required = new[] { "weekday", "start_time", "end_time" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_catalog_link",
                description = "Retorna o link público do catálogo/agenda online do salão para compartilhar com clientes.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_subscription_status",
                description = "Retorna o status da assinatura do plano: se está ativa, em trial, quando expira, quantos dias restam e os passos para renovar ou assinar.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_referral_info",
                description = "Retorna o código e link de indicação do negócio para o programa de indicações.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        }
    ];

    public async Task<AiResponse> ChatAsync(
        AppDbContext db,
        TenantContext tenant,
        Guid adminUserId,
        string userMessage,
        Guid? conversationId,
        string tenantName)
    {
        // Load or create conversation
        var conversation = conversationId.HasValue
            ? await db.AiConversations
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(c => c.Id == conversationId.Value && c.TenantId == tenant.TenantId)
            : null;

        if (conversation is null)
        {
            var title = userMessage.Length > 60 ? userMessage[..57] + "..." : userMessage;
            conversation = new GymApp.Domain.Entities.AiConversation
            {
                TenantId = tenant.TenantId,
                AdminUserId = adminUserId,
                Title = title
            };
            db.AiConversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        // Load the tenant's system prompt
        var tenantData = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
        var systemPrompt = tenantData?.AiSystemPrompt
            ?? $"Você é um assistente virtual do administrador de \"{tenantName}\". " +
               $"Ajude com informações sobre clientes, agendamentos, pacotes e operações do negócio. " +
               $"Seja direto, objetivo e responda sempre em português. " +
               $"A data de hoje é {DateOnly.FromDateTime(DateTime.Today):dd/MM/yyyy}.";

        // Build messages array from history + new message
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        foreach (var m in conversation.Messages)
        {
            if (m.Role == "tool")
            {
                messages.Add(new { role = "tool", tool_call_id = m.ToolName ?? m.Id.ToString(), content = m.ToolResult ?? "" });
            }
            else
            {
                messages.Add(new { role = m.Role, content = m.Content ?? "" });
            }
        }
        messages.Add(new { role = "user", content = userMessage });

        // Persist user message
        var userMsg = new GymApp.Domain.Entities.AiMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = userMessage
        };
        db.AiMessages.Add(userMsg);
        await db.SaveChangesAsync();

        // Tool execution loop
        var model = config["AI:Model"] ?? "mimo-v2-omni";
        var maxTokens = int.TryParse(config["AI:MaxTokens"], out var mt) ? mt : 2048;
        var maxRounds = int.TryParse(config["AI:MaxToolRounds"], out var mr) ? mr : 5;
        string? finalContent = null;
        int totalTokens = 0;

        for (int round = 0; round < maxRounds; round++)
        {
            var requestBody = new
            {
                model,
                messages,
                tools = _tools,
                max_tokens = maxTokens
            };

            var http = httpFactory.CreateClient("MiMo");
            var json = JsonSerializer.Serialize(requestBody, _json);
            var response = await http.PostAsync("/v1/chat/completions",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();
            var raw = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(raw);
            var choice = doc.RootElement.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var finishReason = choice.GetProperty("finish_reason").GetString();

            if (doc.RootElement.TryGetProperty("usage", out var usage) &&
                usage.TryGetProperty("total_tokens", out var tokensEl))
                totalTokens += tokensEl.GetInt32();

            // No tool calls — final text response
            // Note: content can be null (JsonValueKind.Null) when finish_reason is "tool_calls",
            // so we must use TryGetProperty and check for null kind before calling GetString().
            if (finishReason == "stop" || !message.TryGetProperty("tool_calls", out var toolCalls))
            {
                if (message.TryGetProperty("content", out var contentEl) &&
                    contentEl.ValueKind != JsonValueKind.Null)
                    finalContent = contentEl.GetString() ?? "";
                else
                    finalContent = "";
                break;
            }

            // Process tool calls
            var assistantToolMsg = new
            {
                role = "assistant",
                content = message.TryGetProperty("content", out var ac) ? ac.GetString() : null,
                tool_calls = JsonSerializer.Deserialize<object>(toolCalls.GetRawText())
            };
            messages.Add(assistantToolMsg);

            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                var toolCallId = toolCall.GetProperty("id").GetString()!;
                var toolName = toolCall.GetProperty("function").GetProperty("name").GetString()!;
                var toolArgsRaw = toolCall.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                var toolArgs = JsonNode.Parse(toolArgsRaw)?.AsObject();

                var toolResult = await ExecuteToolAsync(db, tenant, toolName, toolArgs, _http);

                // Persist tool message
                var toolMsg = new GymApp.Domain.Entities.AiMessage
                {
                    ConversationId = conversation.Id,
                    Role = "tool",
                    ToolName = toolCallId,
                    ToolInput = toolArgsRaw,
                    ToolResult = toolResult
                };
                db.AiMessages.Add(toolMsg);

                messages.Add(new { role = "tool", tool_call_id = toolCallId, content = toolResult });
            }

            await db.SaveChangesAsync();
        }

        finalContent ??= "Não foi possível processar sua solicitação. Por favor, tente novamente.";

        // Persist assistant response
        var assistantMsg = new GymApp.Domain.Entities.AiMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = finalContent,
            TokensUsed = totalTokens
        };
        db.AiMessages.Add(assistantMsg);
        conversation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new AiResponse(conversation.Id, conversation.Title, finalContent);
    }

    private static async Task<string> ExecuteToolAsync(
        AppDbContext db, TenantContext tenant, string toolName, JsonObject? args, IHttpContextAccessor http)
    {
        try
        {
            return toolName switch
            {
                "list_students"         => await ListStudentsAsync(db, tenant, args),
                "get_student"           => await GetStudentAsync(db, tenant, args),
                "list_sessions"         => await ListSessionsAsync(db, tenant, args),
                "get_dashboard_stats"   => await GetDashboardStatsAsync(db, tenant),
                "list_expiring_packages"=> await ListExpiringPackagesAsync(db, tenant),
                "list_inactive_students"=> await ListInactiveStudentsAsync(db, tenant, args),
                "list_service_categories"  => await ListServiceCategoriesAsync(db, tenant),
                "create_service_category"  => await CreateServiceCategoryAsync(db, tenant, args),
                "update_service_category"  => await UpdateServiceCategoryAsync(db, tenant, args),
                "list_services"            => await ListServicesAsync(db, tenant),
                "create_service"        => await CreateServiceAsync(db, tenant, args),
                "update_service"        => await UpdateServiceAsync(db, tenant, args),
                "list_availability"     => await ListAvailabilityAsync(db, tenant),
                "create_availability"   => await CreateAvailabilityAsync(db, tenant, args),
                "get_catalog_link"      => await GetCatalogLinkAsync(db, tenant, http),
                "get_subscription_status"=> await GetSubscriptionStatusAsync(db, tenant, http),
                "get_referral_info"     => await GetReferralInfoAsync(db, tenant),
                _ => $"Ferramenta desconhecida: {toolName}"
            };
        }
        catch (Exception ex)
        {
            return $"Erro ao executar {toolName}: {ex.Message}";
        }
    }

    private static async Task<string> ListStudentsAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        var search = args?["search"]?.GetValue<string>();
        var statusStr = args?["status"]?.GetValue<string>();

        var query = db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Student);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));

        if (Enum.TryParse<StudentStatus>(statusStr, out var status))
            query = query.Where(u => u.Status == status);

        var students = await query
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.Phone, u.Status, u.CreatedAt })
            .Take(50)
            .ToListAsync();

        return JsonSerializer.Serialize(students);
    }

    private static async Task<string> GetStudentAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        if (!Guid.TryParse(args?["student_id"]?.GetValue<string>(), out var id))
            return "ID de aluno inválido.";

        var user = await db.Users.AsNoTracking()
            .Include(u => u.Packages).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenant.TenantId && u.Role == UserRole.Student);

        if (user is null) return "Aluno não encontrado.";

        var credits = user.Packages.Where(p => p.IsActive).SelectMany(p => p.Items).Sum(i => i.TotalCredits - i.UsedCredits);
        return JsonSerializer.Serialize(new { user.Id, user.Name, user.Email, user.Phone, user.Status, user.BirthDate, user.HealthNotes, RemainingCredits = credits });
    }

    private static async Task<string> ListSessionsAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        var from = DateOnly.TryParse(args?["from"]?.GetValue<string>(), out var f) ? f : DateOnly.FromDateTime(DateTime.Today);
        var to = DateOnly.TryParse(args?["to"]?.GetValue<string>(), out var t) ? t : from.AddDays(13);

        var sessions = await db.Sessions.AsNoTracking()
            .Include(s => s.ClassType)
            .Include(s => s.Bookings)
            .Where(s => s.TenantId == tenant.TenantId && s.Date >= from && s.Date <= to)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id, s.Date, s.StartTime, s.Status,
                ClassType = s.ClassType != null ? s.ClassType.Name : "",
                s.SlotsAvailable,
                Bookings = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
            })
            .ToListAsync();

        return JsonSerializer.Serialize(sessions);
    }

    private static async Task<string> GetDashboardStatsAsync(AppDbContext db, TenantContext tenant)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);
        var startOfMonthUtc = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalStudents = await db.Users.CountAsync(u =>
            u.TenantId == tenant.TenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active);

        var bookingsThisMonth = await db.Bookings.CountAsync(b =>
            b.Session.TenantId == tenant.TenantId && b.Session.Date >= startOfMonth && b.Status != BookingStatus.Cancelled);

        var sessionsToday = await db.Sessions.CountAsync(s =>
            s.TenantId == tenant.TenantId && s.Date == today && s.Status == SessionStatus.Scheduled);

        var revenueThisMonth = await db.PackageItems.AsNoTracking()
            .Where(i => i.Package.TenantId == tenant.TenantId && i.Package.CreatedAt >= startOfMonthUtc)
            .SumAsync(i => i.PricePerCredit * i.TotalCredits);

        var newStudentsThisMonth = await db.Users.CountAsync(u =>
            u.TenantId == tenant.TenantId && u.Role == UserRole.Student && u.CreatedAt >= startOfMonthUtc);

        return JsonSerializer.Serialize(new
        {
            TotalStudents = totalStudents,
            BookingsThisMonth = bookingsThisMonth,
            SessionsToday = sessionsToday,
            RevenueThisMonth = revenueThisMonth,
            NewStudentsThisMonth = newStudentsThisMonth
        });
    }

    private static async Task<string> ListExpiringPackagesAsync(AppDbContext db, TenantContext tenant)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(14);

        var packages = await db.Packages.AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.Items)
            .Where(p => p.TenantId == tenant.TenantId && p.IsActive && p.ExpiresAt.HasValue && p.ExpiresAt <= until)
            .OrderBy(p => p.ExpiresAt)
            .Select(p => new
            {
                p.Id, p.Name, p.ExpiresAt,
                IsExpired = p.ExpiresAt < today,
                StudentName = p.Student.Name,
                StudentEmail = p.Student.Email,
                RemainingCredits = p.Items.Sum(i => i.TotalCredits - i.UsedCredits)
            })
            .ToListAsync();

        return JsonSerializer.Serialize(packages);
    }

    private static async Task<string> ListInactiveStudentsAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        var days = args?["days"]?.GetValue<int>() ?? 14;
        var inactiveSince = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));

        var activeStudents = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenant.TenantId && u.Role == UserRole.Student && u.Status == StudentStatus.Active)
            .Select(u => new { u.Id, u.Name, u.Email, u.Phone })
            .ToListAsync();

        var recentIds = await db.Bookings
            .Where(b => b.Session.TenantId == tenant.TenantId && b.Session.Date >= inactiveSince && b.Status != BookingStatus.Cancelled)
            .Select(b => b.StudentId)
            .Distinct()
            .ToListAsync();

        return JsonSerializer.Serialize(activeStudents.Where(s => !recentIds.Contains(s.Id)));
    }

    private static async Task<string> ListServiceCategoriesAsync(AppDbContext db, TenantContext tenant)
    {
        var cats = await db.ServiceCategories.AsNoTracking()
            .Where(c => c.TenantId == tenant.TenantId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.SortOrder, c.IsActive })
            .ToListAsync();

        return JsonSerializer.Serialize(cats);
    }

    private static async Task<string> CreateServiceCategoryAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        var name = args?["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) return "Campo 'name' é obrigatório.";

        var existing = await db.ServiceCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId &&
                                      c.Name.ToLower() == name.ToLower());
        if (existing is not null)
            return JsonSerializer.Serialize(new
            {
                AlreadyExists = true,
                existing.Id, existing.Name, existing.SortOrder, existing.IsActive,
                Message = $"Categoria '{existing.Name}' já existe. Use update_service_category para atualizar."
            });

        int sortOrder = 0;
        if (args?["sort_order"] is JsonNode sortNode && int.TryParse(sortNode.ToJsonString(), out var s))
            sortOrder = s;

        var cat = new GymApp.Domain.Entities.ServiceCategory
        {
            TenantId = tenant.TenantId,
            Name = name,
            SortOrder = sortOrder
        };
        db.ServiceCategories.Add(cat);
        await db.SaveChangesAsync();

        return JsonSerializer.Serialize(new { cat.Id, cat.Name, cat.SortOrder, cat.IsActive });
    }

    private static async Task<string> UpdateServiceCategoryAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        if (!Guid.TryParse(args?["category_id"]?.GetValue<string>(), out var id))
            return "ID de categoria inválido.";

        var cat = await db.ServiceCategories
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId);
        if (cat is null) return "Categoria não encontrada.";

        cat.Name = args?["name"]?.GetValue<string>() ?? cat.Name;

        if (args?["sort_order"] is JsonNode sortNode && int.TryParse(sortNode.ToJsonString(), out var s))
            cat.SortOrder = s;
        if (args?["is_active"] is JsonNode activeNode && bool.TryParse(activeNode.ToJsonString(), out var active))
            cat.IsActive = active;

        await db.SaveChangesAsync();

        return JsonSerializer.Serialize(new { cat.Id, cat.Name, cat.SortOrder, cat.IsActive });
    }

    private static async Task<string> ListServicesAsync(AppDbContext db, TenantContext tenant)
    {
        var services = await db.ClassTypes.AsNoTracking()
            .Include(ct => ct.Category)
            .Where(ct => ct.TenantId == tenant.TenantId)
            .OrderBy(ct => ct.Name)
            .Select(ct => new
            {
                ct.Id, ct.Name, ct.Description, ct.ModalityType,
                ct.IsActive, ct.Price, ct.DurationMinutes, ct.Color,
                CategoryName = ct.Category != null ? ct.Category.Name : null
            })
            .ToListAsync();

        return JsonSerializer.Serialize(services);
    }

    private static async Task<string> CreateServiceAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        var name = args?["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name)) return "Campo 'name' é obrigatório.";

        var existing = await db.ClassTypes.AsNoTracking()
            .FirstOrDefaultAsync(ct => ct.TenantId == tenant.TenantId &&
                                       ct.Name.ToLower() == name.ToLower());
        if (existing is not null)
            return JsonSerializer.Serialize(new
            {
                AlreadyExists = true,
                existing.Id, existing.Name, existing.Price,
                existing.DurationMinutes, existing.ModalityType, existing.IsActive,
                Message = $"Serviço '{existing.Name}' já existe. Use update_service para atualizar."
            });

        if (!Enum.TryParse<ModalityType>(args?["modality_type"]?.GetValue<string>(), out var modality))
            modality = ModalityType.Individual;

        decimal? price = null;
        if (args?["price"] is JsonNode priceNode && decimal.TryParse(priceNode.ToJsonString(), out var p))
            price = p;

        int? duration = null;
        if (args?["duration_minutes"] is JsonNode durNode && int.TryParse(durNode.ToJsonString(), out var d))
            duration = d;

        var color = args?["color"]?.GetValue<string>() ?? "#6c757d";
        var description = args?["description"]?.GetValue<string>();

        Guid? categoryId = null;
        var categoryName = args?["category_name"]?.GetValue<string>();
        string? resolvedCategoryName = null;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var cat = await db.ServiceCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId &&
                                          c.Name.ToLower() == categoryName.ToLower());
            if (cat is null)
                return $"Categoria '{categoryName}' não encontrada. Use list_service_categories para ver as disponíveis ou create_service_category para criar.";
            categoryId = cat.Id;
            resolvedCategoryName = cat.Name;
        }

        var service = new GymApp.Domain.Entities.ClassType
        {
            TenantId = tenant.TenantId,
            Name = name,
            Description = description,
            Color = color,
            ModalityType = modality,
            Price = price,
            DurationMinutes = duration,
            CategoryId = categoryId
        };
        db.ClassTypes.Add(service);
        await db.SaveChangesAsync();

        return JsonSerializer.Serialize(new { service.Id, service.Name, service.Price, service.DurationMinutes, service.ModalityType, service.IsActive, CategoryName = resolvedCategoryName });
    }

    private static async Task<string> UpdateServiceAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        if (!Guid.TryParse(args?["service_id"]?.GetValue<string>(), out var id))
            return "ID de serviço inválido.";

        var service = await db.ClassTypes.FirstOrDefaultAsync(ct => ct.Id == id && ct.TenantId == tenant.TenantId);
        if (service is null) return "Serviço não encontrado.";

        service.Name = args?["name"]?.GetValue<string>() ?? service.Name;
        service.Description = args?["description"]?.GetValue<string>() ?? service.Description;
        service.Color = args?["color"]?.GetValue<string>() ?? service.Color;

        if (args?["price"] is JsonNode priceNode && decimal.TryParse(priceNode.ToJsonString(), out var p))
            service.Price = p;
        if (args?["duration_minutes"] is JsonNode durNode && int.TryParse(durNode.ToJsonString(), out var d))
            service.DurationMinutes = d;
        if (args?["is_active"] is JsonNode activeNode && bool.TryParse(activeNode.ToJsonString(), out var active))
            service.IsActive = active;
        if (Enum.TryParse<ModalityType>(args?["modality_type"]?.GetValue<string>(), out var modality))
            service.ModalityType = modality;

        string? resolvedCategoryName = null;
        if (args?.ContainsKey("category_name") == true)
        {
            var categoryName = args["category_name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                service.CategoryId = null;
            }
            else
            {
                var cat = await db.ServiceCategories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId &&
                                              c.Name.ToLower() == categoryName.ToLower());
                if (cat is null)
                    return $"Categoria '{categoryName}' não encontrada. Use list_service_categories para ver as disponíveis.";
                service.CategoryId = cat.Id;
                resolvedCategoryName = cat.Name;
            }
        }

        await db.SaveChangesAsync();

        return JsonSerializer.Serialize(new { service.Id, service.Name, service.Price, service.DurationMinutes, service.ModalityType, service.IsActive, CategoryName = resolvedCategoryName });
    }

    private static async Task<string> ListAvailabilityAsync(AppDbContext db, TenantContext tenant)
    {
        var days = new[] { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };

        var blocks = await db.ProfessionalAvailability.AsNoTracking()
            .Include(a => a.Instructor).ThenInclude(i => i!.User)
            .Where(a => a.TenantId == tenant.TenantId && a.IsActive)
            .OrderBy(a => a.Weekday).ThenBy(a => a.StartTime)
            .Select(a => new
            {
                a.Id,
                Weekday = a.Weekday,
                WeekdayName = days[a.Weekday],
                StartTime = a.StartTime.ToString("HH:mm"),
                EndTime = a.EndTime.ToString("HH:mm"),
                InstructorName = a.Instructor != null ? a.Instructor.User.Name : null
            })
            .ToListAsync();

        return JsonSerializer.Serialize(blocks);
    }

    private static async Task<string> CreateAvailabilityAsync(AppDbContext db, TenantContext tenant, JsonObject? args)
    {
        if (!int.TryParse(args?["weekday"]?.ToJsonString(), out var weekday) || weekday < 0 || weekday > 6)
            return "Campo 'weekday' inválido (0=Dom até 6=Sáb).";

        if (!TimeOnly.TryParse(args?["start_time"]?.GetValue<string>(), out var start))
            return "Campo 'start_time' inválido. Use formato HH:MM.";

        if (!TimeOnly.TryParse(args?["end_time"]?.GetValue<string>(), out var end))
            return "Campo 'end_time' inválido. Use formato HH:MM.";

        if (start >= end) return "O horário de início deve ser anterior ao horário de fim.";

        var dayNames = new[] { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" };
        var existing = await db.ProfessionalAvailability.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenant.TenantId &&
                                      a.IsActive &&
                                      a.Weekday == weekday &&
                                      a.StartTime == start &&
                                      a.EndTime == end);
        if (existing is not null)
            return JsonSerializer.Serialize(new
            {
                AlreadyExists = true,
                existing.Id,
                DayName = dayNames[weekday],
                StartTime = start.ToString("HH:mm"),
                EndTime = end.ToString("HH:mm"),
                Message = $"Disponibilidade {dayNames[weekday]} {start:HH:mm}–{end:HH:mm} já está cadastrada."
            });

        var block = new GymApp.Domain.Entities.ProfessionalAvailability
        {
            TenantId = tenant.TenantId,
            Weekday = weekday,
            StartTime = start,
            EndTime = end
        };
        db.ProfessionalAvailability.Add(block);
        await db.SaveChangesAsync();

        var dayName = new[] { "Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado" }[weekday];
        return JsonSerializer.Serialize(new { block.Id, DayName = dayName, StartTime = start.ToString("HH:mm"), EndTime = end.ToString("HH:mm") });
    }

    private static async Task<string> GetCatalogLinkAsync(AppDbContext db, TenantContext tenant, IHttpContextAccessor http)
    {
        var tenantData = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
        if (tenantData is null) return "Tenant não encontrado.";

        string catalogUrl;

        if (!string.IsNullOrWhiteSpace(tenantData.CustomDomain))
        {
            catalogUrl = $"https://{tenantData.CustomDomain}/catalog";
        }
        else
        {
            var requestHost = http.HttpContext?.Request.Host.Host ?? "";
            var parts = requestHost.Split('.');
            // "boxe-elite.gymapp.com" → baseDomain = "gymapp.com"
            var baseDomain = parts.Length >= 3
                ? string.Join('.', parts.Skip(1))
                : requestHost;

            var scheme = http.HttpContext?.Request.Scheme ?? "https";
            catalogUrl = $"{scheme}://{tenantData.Slug}.{baseDomain}/catalog";
        }

        return JsonSerializer.Serialize(new { CatalogUrl = catalogUrl });
    }

    private static async Task<string> GetSubscriptionStatusAsync(AppDbContext db, TenantContext tenant, IHttpContextAccessor http)
    {
        var tenantData = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
        if (tenantData is null) return "Tenant não encontrado.";

        var now = DateTime.UtcNow;
        var trialEnd = tenantData.CreatedAt.AddDays(tenantData.TrialDays);
        var isInTrial = tenantData.SubscriptionStatus == SubscriptionStatus.Trial && now < trialEnd;

        int? daysRemaining = tenantData.SubscriptionCurrentPeriodEnd.HasValue
            ? Math.Max(0, (int)(tenantData.SubscriptionCurrentPeriodEnd.Value - now).TotalDays)
            : isInTrial
                ? Math.Max(0, (int)(trialEnd - now).TotalDays)
                : null;

        // Build billing page URL from the current request host
        var requestHost = http.HttpContext?.Request.Host.Host ?? "";
        var scheme = http.HttpContext?.Request.Scheme ?? "https";
        var parts = requestHost.Split('.');
        var baseDomain = parts.Length >= 3 ? string.Join('.', parts.Skip(1)) : requestHost;
        var billingPageUrl = $"{scheme}://{tenantData.Slug}.{baseDomain}/admin#billing";

        // Contextual renewal instructions based on status
        var status = tenantData.SubscriptionStatus;
        string renewalInstructions = status switch
        {
            SubscriptionStatus.Trial => isInTrial
                ? $"Você está no período de trial com {daysRemaining} dia(s) restante(s). Para assinar antes do trial expirar: 1) Acesse a página de Assinatura em {billingPageUrl} 2) Clique em 'Assinar agora' 3) Preencha seus dados e CPF/CNPJ 4) Você será redirecionado para o pagamento via AbacatePay 5) Após o pagamento confirmado, a assinatura é ativada automaticamente."
                : $"Seu trial expirou. Para reativar o acesso: 1) Acesse {billingPageUrl} 2) Clique em 'Assinar' 3) Preencha seus dados e CPF/CNPJ 4) Realize o pagamento via AbacatePay.",
            SubscriptionStatus.Active =>
                $"Assinatura ativa até {tenantData.SubscriptionCurrentPeriodEnd?.ToString("dd/MM/yyyy")} ({daysRemaining} dia(s) restante(s)). A renovação não é automática — quando o período expirar será necessário realizar um novo pagamento. Para gerenciar ou cancelar, acesse: {billingPageUrl}",
            SubscriptionStatus.Canceled =>
                $"Assinatura cancelada. Para reativar: 1) Acesse {billingPageUrl} 2) Clique em 'Reativar assinatura' 3) Realize o pagamento via AbacatePay.",
            SubscriptionStatus.PastDue =>
                $"Pagamento pendente ou falhou. Para regularizar: 1) Acesse {billingPageUrl} 2) Clique em 'Pagar agora' 3) Realize o pagamento via AbacatePay. Seu acesso será restaurado automaticamente após a confirmação.",
            _ =>
                $"Para gerenciar sua assinatura acesse: {billingPageUrl}"
        };

        return JsonSerializer.Serialize(new
        {
            Status = status.ToString(),
            IsInTrial = isInTrial,
            TrialEndsAt = isInTrial ? trialEnd.ToString("dd/MM/yyyy") : null,
            PeriodEndsAt = tenantData.SubscriptionCurrentPeriodEnd?.ToString("dd/MM/yyyy"),
            DaysRemaining = daysRemaining,
            IsActive = tenantData.HasStudentAccess,
            BillingPageUrl = billingPageUrl,
            RenewalInstructions = renewalInstructions
        });
    }

    private static async Task<string> GetReferralInfoAsync(AppDbContext db, TenantContext tenant)
    {
        var tenantData = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId);
        if (tenantData is null) return "Tenant não encontrado.";

        var referrals = await db.Tenants.AsNoTracking()
            .Where(t => t.ReferredByTenantId == tenant.TenantId)
            .Select(t => new { t.ReferralRewardClaimed })
            .ToListAsync();

        return JsonSerializer.Serialize(new
        {
            ReferralCode = tenantData.Slug,
            TotalReferrals = referrals.Count,
            ConvertedReferrals = referrals.Count(r => r.ReferralRewardClaimed),
            Description = $"Código de indicação: {tenantData.Slug}. Compartilhe em: https://agendofy.com?ref={tenantData.Slug}"
        });
    }
}

public record AiResponse(Guid ConversationId, string ConversationTitle, string Message);
