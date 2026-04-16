using System.Net.Http.Json;
using System.Text.Json;

namespace FMDesktop.Api;

public static class ApiClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("http://localhost:8081/api/"),
        Timeout     = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T?> GetAsync<T>(string path)
    {
        try
        {
            return await Http.GetFromJsonAsync<T>(path, JsonOpts);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ApiClient] GET {path} fehlgeschlagen: {e.Message}");
            return default;
        }
    }

    public static async Task<bool> PostAsync(string path)
    {
        try
        {
            var response = await Http.PostAsync(path, null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ApiClient] POST {path} fehlgeschlagen: {e.Message}");
            return false;
        }
    }
}
