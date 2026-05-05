using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GymApp.Api.Services;

public record AbacatePayCustomer(string Id);

// V2 customer/create response wraps fields inside "metadata"
public record AbacatePayCustomerV2Response(string Id, AbacatePayCustomerMetadata? Metadata);
public record AbacatePayCustomerMetadata(string Name, string Email);

public record AbacatePayProduct(string Id, string Name, int Price, string? Cycle, string Status);

public record AbacatePaySubscription(string Id, string Url, string Status);

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

    private HttpClient CreateClient(string? apiKey = null)
    {
        apiKey ??= config["AbacatePay:ApiKey"]
            ?? throw new InvalidOperationException("AbacatePay:ApiKey not configured.");

        var client = new HttpClient { BaseAddress = new Uri("https://api.abacatepay.com/v2/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    // ── Tenant student-payment methods (use tenant's own API key) ─────────────

    public Task<AbacatePayCustomer?> CreateStudentCustomerAsync(
        string apiKey, string name, string email) =>
        CreateCustomerCoreAsync(CreateClient(apiKey), name, email, null, null);

    // Normalizes payment method to V2 values: "CARD" or "PIX"
    public static string[] NormalizeMethods(string[]? methods) =>
        methods?.Select(m => m.ToUpperInvariant() == "CREDIT_CARD" ? "CARD" : m.ToUpperInvariant()).ToArray()
        ?? ["PIX"];

    public async Task<AbacatePayBilling?> CreateStudentBillingAsync(
        string apiKey, string customerId, string productName, string studentName, int priceCents, string returnUrl,
        string[]? methods = null)
    {
        using var client = CreateClient(apiKey);
        methods = NormalizeMethods(methods);

        var displayName = $"{productName} — {studentName}";
        var body = new
        {
            frequency = "ONE_TIME",
            methods,
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

        var jsonBody = JsonSerializer.Serialize(body, JsonOpts);
        logger.LogDebug("AbacatePay CreateStudentBilling request: {Body}", jsonBody);

        var response = await client.PostAsync("billings/create",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));

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

        var jsonBody = JsonSerializer.Serialize(body, JsonOpts);
        logger.LogDebug("AbacatePay CreateCustomer request: {Body}", jsonBody);

        var response = await client.PostAsync("customers/create",
            new StringContent(jsonBody, Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();
        logger.LogDebug("AbacatePay CreateCustomer response: {Status} {Body}", response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("AbacatePay CreateCustomer failed: {Status} {Body}", response.StatusCode, responseBody);
            return null;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data");
        var v2 = JsonSerializer.Deserialize<AbacatePayCustomerV2Response>(data.GetRawText(), JsonOpts);
        return v2 is null ? null : new AbacatePayCustomer(v2.Id);
    }

    public async Task<AbacatePayBilling?> CreateBillingAsync(
        string customerId, string tenantSlug, string tenantName, string returnUrl,
        int? priceCents = null, string[]? methods = null)
    {
        using var client = CreateClient();
        var subscriptionPrice = priceCents ?? config.GetValue<int>("AbacatePay:SubscriptionPriceCents", 4900);
        var normalizedMethods = NormalizeMethods(methods);

        var body = new
        {
            frequency = "MULTIPLE_PAYMENTS",
            methods = normalizedMethods,
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

        var response = await client.PostAsync("billings/create",
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

    // ── V2 Subscription flow ───────────────────────────────────────────────────

    public async Task<AbacatePayProduct?> CreateSubscriptionProductAsync(
        string externalId, string name, int priceCents, string description)
    {
        using var client = CreateClient();

        var body = new
        {
            externalId,
            name,
            price = priceCents,
            currency = "BRL",
            description,
            cycle = "MONTHLY"
        };

        var response = await client.PostAsync("products/create",
            new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("AbacatePay CreateSubscriptionProduct failed: {Status} {Body}", response.StatusCode, responseBody);
            return null;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<AbacatePayProduct>(data.GetRawText(), JsonOpts);
    }

    public async Task<AbacatePaySubscription?> CreateSubscriptionAsync(
        string productId, string? customerId, string returnUrl)
    {
        using var client = CreateClient();

        var body = new
        {
            items = new[] { new { id = productId, quantity = 1 } },
            methods = new[] { "CARD" },
            customerId,
            returnUrl,
            completionUrl = returnUrl
        };

        var response = await client.PostAsync("subscriptions/create",
            new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json"));

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("AbacatePay CreateSubscription failed: {Status} {Body}", response.StatusCode, responseBody);
            return null;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<AbacatePaySubscription>(data.GetRawText(), JsonOpts);
    }

    public async Task<bool> CancelBillingAsync(string billingId)
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync($"billings/{billingId}");

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            logger.LogError("AbacatePay CancelBilling failed: {Status} {Body}", response.StatusCode, err);
            return false;
        }

        return true;
    }
}
