namespace ResumeTailorAI.Web.Models;

/// <summary>Output of a full tailoring run, including each agent's intermediate reasoning.</summary>
public class TailorResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public string TailoredResume { get; set; } = string.Empty;
    public string JobAnalysis { get; set; } = string.Empty;
    public string MatchStrategy { get; set; } = string.Empty;

    public long DurationMs { get; set; }
    public string Model { get; set; } = string.Empty;
}
