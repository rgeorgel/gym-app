using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace GymApp.Api.Services;

public class EfiOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>Base64-encoded .p12/.pfx certificate for mTLS</summary>
    public string CertificateBase64 { get; set; } = string.Empty;
    public string CertificatePassword { get; set; } = string.Empty;
    /// <summary>Platform's PIX key (chave) used as the charge recipient</summary>
    public string PixKey { get; set; } = string.Empty;
    /// <summary>Platform's own payee_code — used for the split fee slice</summary>
    public string? PlatformPayeeCode { get; set; }
    /// <summary>Percentage the platform keeps (0 = no split, all goes to academia via its payee code)</summary>
    public decimal PlatformFeePercent { get; set; } = 0;
    public bool Sandbox { get; set; } = true;
}

public record EfiChargeResult(string TxId, string PixCopyPaste, string QrCodeBase64);

public class EfiService(EfiOptions opts, ILogger<EfiService> logger)
{
    private string BaseUrl => opts.Sandbox
        ? "https://pix-h.api.efipay.com.br"
        : "https://pix.api.efipay.com.br";

    public bool IsConfigured =>
        !string.IsNullOrEmpty(opts.ClientId) &&
        !string.IsNullOrEmpty(opts.ClientSecret) &&
        !string.IsNullOrEmpty(opts.PixKey);

    private HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();

        if (!string.IsNullOrEmpty(opts.CertificateBase64))
        {
            var certBytes = Convert.FromBase64String(opts.CertificateBase64);
            var flags = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? X509KeyStorageFlags.EphemeralKeySet
                : X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable;
            var cert = new X509Certificate2(certBytes, opts.CertificatePassword, flags);
            handler.ClientCertificates.Add(cert);
        }

        if (opts.Sandbox)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        return new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
    }

    private async Task<string> GetTokenAsync(HttpClient client)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{opts.ClientId}:{opts.ClientSecret}"));

        using var req = new HttpRequestMessage(HttpMethod.Post, "/oauth/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { grant_type = "client_credentials" }),
            Encoding.UTF8, "application/json");

        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    public async Task<EfiChargeResult> CreatePixChargeAsync(
        string studentName, decimal amount, string description, string? tenantPayeeCode)
    {
        using var client = CreateClient();
        var token = await GetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Generate a unique txid (alphanumeric, 26–35 chars)
        var txId = Guid.NewGuid().ToString("N")[..35];

        var chargeObj = new Dictionary<string, object>
        {
            ["calendario"] = new { expiracao = 3600 },
            ["devedor"] = new { nome = studentName.Length > 60 ? studentName[..60] : studentName },
            ["valor"] = new { original = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) },
            ["chave"] = opts.PixKey,
            ["infoAdicionais"] = new[] { new { nome = "Plano", valor = description.Length > 50 ? description[..50] : description } }
        };

        // Add split if both the academia and platform have payee codes configured
        if (!string.IsNullOrEmpty(tenantPayeeCode) &&
            !string.IsNullOrEmpty(opts.PlatformPayeeCode) &&
            opts.PlatformFeePercent > 0 &&
            opts.PlatformFeePercent < 100)
        {
            var academyPct = (100 - opts.PlatformFeePercent).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var platformPct = opts.PlatformFeePercent.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            chargeObj["split"] = new
            {
                tipoDivisao = "SecureEnvShare",
                descricao = description.Length > 50 ? description[..50] : description,
                lancamento = new { imediato = true },
                divisaoEntreRecebedores = new[]
                {
                    new { conta = new { payeeCode = tenantPayeeCode }, componente = "PERCENTUAL", percentual = academyPct },
                    new { conta = new { payeeCode = opts.PlatformPayeeCode }, componente = "PERCENTUAL", percentual = platformPct }
                }
            };
        }

        var body = new StringContent(
            JsonSerializer.Serialize(chargeObj), Encoding.UTF8, "application/json");

        var chargeRes = await client.PutAsync($"/v2/cob/{txId}", body);

        if (!chargeRes.IsSuccessStatusCode)
        {
            var err = await chargeRes.Content.ReadAsStringAsync();
            logger.LogError("Efí charge failed: {Status} {Body}", chargeRes.StatusCode, err);
            throw new InvalidOperationException($"Efí charge error: {chargeRes.StatusCode}");
        }

        var chargeJson = await chargeRes.Content.ReadFromJsonAsync<JsonElement>();
        var locId = chargeJson.GetProperty("loc").GetProperty("id").GetInt32();
        var pixCopyPaste = chargeJson.GetProperty("pixCopiaECola").GetString()!;
        var returnedTxId = chargeJson.GetProperty("txid").GetString()!;

        // Fetch QR code image
        var qrRes = await client.GetAsync($"/v2/loc/{locId}/qrcode");
        qrRes.EnsureSuccessStatusCode();
        var qrJson = await qrRes.Content.ReadFromJsonAsync<JsonElement>();
        var qrCodeBase64 = qrJson.GetProperty("imagemQrcode").GetString()!;

        return new EfiChargeResult(returnedTxId, pixCopyPaste, qrCodeBase64);
    }
}
