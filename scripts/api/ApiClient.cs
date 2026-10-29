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

    /// Für ausgehende Bodies: Das Backend bindet mit Jackson und damit auf die camelCase-Namen
    /// seiner Java-Felder. Ohne diese Richtlinie schickt System.Text.Json PascalCase.
    private static readonly JsonSerializerOptions SendeOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<T?> GetAsync<T>(string path)
    {
        try
        {
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("X-Schema", CurrentSchema);
            var response = await Http.SendAsync(request);
            if (!DarfLesen(path, response)) return default;
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

    public static async Task<TResult?> PutAsync<TBody, TResult>(string path, TBody body)
    {
        try
        {
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Put, path);
            request.Headers.TryAddWithoutValidation("X-Schema", CurrentSchema);
            request.Content = JsonContent.Create(body, options: SendeOpts);
            var response = await Http.SendAsync(request);
            if (!DarfLesen(path, response)) return default;
            if (response.Content.Headers.ContentLength is null or 0) return default;
            return await response.Content.ReadFromJsonAsync<TResult>(JsonOpts);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ApiClient] PUT {path} fehlgeschlagen: {e.Message}");
            return default;
        }
    }

    /// <summary>
    /// Ob sich das Lesen der Antwort lohnt - und meldet, wenn nicht.
    ///
    /// <para>Vorher wurde jeder Fehlerstatus stillschweigend zu <c>null</c>. Ein Endpunkt, den
    /// ein nicht neu gestartetes Backend gar nicht kennt, sah damit genauso aus wie eine leere
    /// Antwort, und der Grund stand nirgends im Log.</para>
    ///
    /// <para>204 ist kein Fehler, sondern die uebliche Antwort auf eine Anfrage ohne Ergebnis -
    /// dort gibt es nur nichts zu lesen.</para>
    /// </summary>
    private static bool DarfLesen(string path, System.Net.Http.HttpResponseMessage antwort)
    {
        if (antwort.StatusCode == System.Net.HttpStatusCode.NoContent) return false;
        if (antwort.IsSuccessStatusCode) return true;

        GD.PrintErr($"[ApiClient] {path}: HTTP {(int)antwort.StatusCode} {antwort.StatusCode}"
                    + (antwort.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? " - kennt der Server diesen Pfad? Backend neu gestartet?"
                        : ""));
        return false;
    }

    /// <summary>
    /// Wie <see cref="PostAsync{TBody,TResult}"/>, gibt aber zusätzlich den Status zurück.
    ///
    /// <para>Für Aufrufe, bei denen „nichts da“ und „ging schief“ verschiedene Dinge sind: Ein
    /// spielfreier Tag antwortet mit 204, ein abgestürzter Server gar nicht. Beides zu
    /// <c>null</c> zu machen, hat eine leere Ansicht erzeugt, die keinen der beiden Fälle
    /// erklärte.</para>
    /// </summary>
    public static async Task<(TResult? Wert, int Status)> PostMitStatus<TBody, TResult>(
        string path, TBody body)
    {
        try
        {
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("X-Schema", CurrentSchema);
            request.Content = JsonContent.Create(body, options: SendeOpts);
            var response = await Http.SendAsync(request);
            int status = (int)response.StatusCode;

            if (!DarfLesen(path, response)) return (default, status);
            return (await response.Content.ReadFromJsonAsync<TResult>(JsonOpts), status);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ApiClient] POST {path} fehlgeschlagen: {e.Message}");
            return (default, 0);
        }
    }

    public static async Task<TResult?> PostAsync<TBody, TResult>(string path, TBody body)
    {
        try
        {
            var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("X-Schema", CurrentSchema);
            request.Content = JsonContent.Create(body, options: SendeOpts);
            var response = await Http.SendAsync(request);
            if (!DarfLesen(path, response)) return default;
            return await response.Content.ReadFromJsonAsync<TResult>(JsonOpts);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ApiClient] POST {path} fehlgeschlagen: {e.Message}");
            return default;
        }
    }
}
