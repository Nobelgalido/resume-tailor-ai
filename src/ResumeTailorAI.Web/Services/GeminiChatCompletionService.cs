using System.Net.Http.Json;
using System.Text.Json;

namespace ResumeTailorAI.Web.Services;

/// <summary>
/// Talks to the Gemini REST API (generateContent) directly over HttpClient — no SDK,
/// which keeps the dependency surface small and the calls easy to reason about.
/// </summary>
public class GeminiChatCompletionService : IChatCompletionService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public string Model { get; }

    public GeminiChatCompletionService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"] ?? string.Empty;
        Model = config["Gemini:Model"] ?? "gemini-2.5-flash";
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException(
                "Gemini API key is not configured. Set the Gemini__ApiKey environment variable.");

        // Property names are lowercase-first, so the Web serializer leaves them untouched
        // and they map straight onto the fields the Gemini REST API expects.
        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new { temperature = 0.4, maxOutputTokens = 4096 }
        };

        var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";

        using var response = await _http.PostAsJsonAsync(url, payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new GeminiRateLimitException(
                    "Gemini API rate limit reached — the quota for this API key has been used up. " +
                    "Wait a bit and try again, or check your usage at https://aistudio.google.com/app/apikey.");

            throw new HttpRequestException(
                $"Gemini API returned {(int)response.StatusCode}. {Truncate(body, 400)}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            var reason = root.TryGetProperty("promptFeedback", out var pf)
                ? pf.ToString()
                : "the model returned no candidates";
            throw new InvalidOperationException($"Gemini returned no content ({reason}).");
        }

        var text = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return text?.Trim() ?? string.Empty;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}

/// <summary>
/// Thrown when Gemini responds with 429 (quota/rate limit exceeded), so callers can
/// show a specific, friendlier message instead of a raw API error body.
/// </summary>
public class GeminiRateLimitException : Exception
{
    public GeminiRateLimitException(string message) : base(message) { }
}
