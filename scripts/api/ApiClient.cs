#nullable enable
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace FMDesktop.Api;

public static class ApiClient
{
    // Aktives Schema: db_default für die Standard-Datenbank.
    // Wird später durch das gewählte Spielstand-Schema ersetzt.
    public static string CurrentSchema { get; set; } = "db_default";

    private static readonly System.Net.Http.HttpClient Http = new()
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
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("X-Schema", CurrentSchema);
            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOpts);
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
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("X-Schema", CurrentSchema);
            var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ApiClient] POST {path} fehlgeschlagen: {e.Message}");
            return false;
        }
    }
}
