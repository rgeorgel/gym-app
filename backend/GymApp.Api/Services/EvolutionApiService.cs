using System.Net.Http.Json;

namespace GymApp.Api.Services;

public class EvolutionApiService(IHttpClientFactory factory, IConfiguration config)
{
    private readonly string _apiKey = config["Evolution:ApiKey"] ?? string.Empty;

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("apikey", _apiKey);
        return req;
    }

    /// <summary>Creates a WhatsApp instance in Evolution API. Returns the instance name.</summary>
    public async Task CreateInstanceAsync(string instanceName, string phoneNumber)
    {
        using var http = factory.CreateClient("Evolution");
        var req = Request(HttpMethod.Post, "/instance/create");
        req.Content = JsonContent.Create(new
        {
            instanceName,
            number = phoneNumber.Replace("+", "").Replace(" ", "").Replace("-", ""),
            integration = "WHATSAPP-BAILEYS",
            qrcode = false
        });
        var response = await http.SendAsync(req);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Evolution API error ({response.StatusCode}): {body}");
        }
    }

    /// <summary>Deletes a WhatsApp instance from Evolution API.</summary>
    public async Task DeleteInstanceAsync(string instanceName)
    {
        using var http = factory.CreateClient("Evolution");
        var req = Request(HttpMethod.Delete, $"/instance/delete/{instanceName}");
        await http.SendAsync(req); // best-effort, ignore errors
    }

    /// <summary>Fetches the current connection state for an instance.</summary>
    public async Task<string?> GetInstanceStateAsync(string instanceName)
    {
        using var http = factory.CreateClient("Evolution");
        var req = Request(HttpMethod.Get, $"/instance/connectionState/{instanceName}");
        var response = await http.SendAsync(req);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<EvolutionStateResult>();
        return result?.State;
    }
}

file sealed class EvolutionStateResult
{
    public string? State { get; set; }
}
