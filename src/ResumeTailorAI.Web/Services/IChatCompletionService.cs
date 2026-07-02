namespace ResumeTailorAI.Web.Services;

/// <summary>
/// A single-turn chat completion. Kept deliberately provider-agnostic so the
/// Gemini implementation can be swapped for Claude/OpenAI without touching the pipeline.
/// </summary>
public interface IChatCompletionService
{
    string Model { get; }

    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
