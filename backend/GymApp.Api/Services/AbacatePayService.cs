using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GymApp.Api.Services;

public record AbacatePayCustomer(
    string Id,
    string Name,
    string Email
);

public record AbacatePayBilling(
    string Id,
    string Url,
    string Status,
    string? CustomerId
);

public record AbacatePayWebhookEvent(
    string Event,
    AbacatePayWebhookData? Data
);

public record AbacatePayWebhookData(
    AbacatePayWebhookBilling? Billing
);

public record AbacatePayWebhookBilling(
    string Id,
    string? CustomerId,
    string Status,
    DateTime? NextBilling
);

public class AbacatePayService(IConfiguration config, ILogger<AbacatePayService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private HttpClient CreateClient(string? apiKey = null, string version = "v1")
    {
        apiKey ??= config["AbacatePay:ApiKey"]
            ?? throw new InvalidOperationException("AbacatePay:ApiKey not configured.");

        var client = new HttpClient { BaseAddress = new Uri($"https://api.abacatepay.com/{version}/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    // ── Tenant student-payment methods (use tenant's own API key) ─────────────

    public Task<AbacatePayCustomer?> CreateStudentCustomerAsync(
        string apiKey, string name, string email) =>
        CreateCustomerCoreAsync(CreateClient(apiKey), name, email, null, null);

    public async Task<AbacatePayBilling?> CreateStudentBillingAsync(
        string apiKey, string customerId, string productName, string studentName, int priceCents, string returnUrl)
    {
        using var client = CreateClient(apiKey);

        var displayName = $"{productName} — {studentName}";
        var body = new
        {
            frequency = "ONE_TIME",
            methods = new[] { "PIX" },
            products = new[]
            {
                new
                {
                    externalId = $"pay-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                    name = displayName,
                    description = displayName,
                    quantity = 1,
                    price = priceCents
                }
            },
            customerId,
            returnUrl,
            completionUrl = returnUrl
        };

        var response = await client.PostAsync("billing/create",
            new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            logger.LogError("AbacatePay CreateStudentBilling failed: {Status} {Body}", response.StatusCode, err);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<AbacatePayBilling>(data.GetRawText(), JsonOpts);
    }

    // ── Platform (subscription) methods ────────────────────────────────────────

    public async Task<AbacatePayCustomer?> CreateCustomerAsync(
        string name, string email, string? phone, string? taxId)
    {
        using var client = CreateClient();
        return await CreateCustomerCoreAsync(client, name, email, phone, taxId);
    }

    private async Task<AbacatePayCustomer?> CreateCustomerCoreAsync(
        HttpClient client, string name, string email, string? phone, string? taxId)
    {
        var body = new
        {
            name,
            email,
            cellphone = phone ?? string.Empty,
            taxId = taxId ?? string.Empty
        };

        var response = await client.PostAsync("customer/create",
            new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("AbacatePay CreateCustomer failed: {Status} {Body}", response.StatusCode, responseBody);
            return null;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<AbacatePayCustomer>(data.GetRawText(), JsonOpts);
    }

    public async Task<AbacatePayBilling?> CreateBillingAsync(
        string customerId, string tenantSlug, string tenantName, string adminEmail, string baseUrl)
    {
        using var client = CreateClient();
        var subscriptionPrice = config.GetValue<int>("AbacatePay:SubscriptionPriceCents", 4900);
        var returnUrl = $"{baseUrl.TrimEnd('/')}/admin/index.html#billing";

        var body = new
        {
            frequency = "MULTIPLE_PAYMENTS",
            methods = new[] { "PIX" },
            products = new[]
            {
                new
                {
                    externalId = $"sub-{tenantSlug}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                    name = $"Assinatura Agendofy — {tenantName}",
                    description = $"Mensalidade do sistema de agendamento para academias — {tenantName}",
                    quantity = 1,
                    price = subscriptionPrice
                }
            },
            customerId,
            returnUrl,
            completionUrl = returnUrl
        };

        var response = await client.PostAsync("billing/create",
            new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            logger.LogError("AbacatePay CreateBilling failed: {Status} {Body}", response.StatusCode, err);
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<AbacatePayBilling>(data.GetRawText(), JsonOpts);
    }

    public async Task<bool> CancelBillingAsync(string billingId)
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync($"billing/{billingId}");

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            logger.LogError("AbacatePay CancelBilling failed: {Status} {Body}", response.StatusCode, err);
            return false;
        }

        return true;
    }
}
