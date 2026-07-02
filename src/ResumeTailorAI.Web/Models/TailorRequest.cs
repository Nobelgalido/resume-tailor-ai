namespace ResumeTailorAI.Web.Models;

/// <summary>Input for a tailoring run: the candidate's resume and the target job posting.</summary>
public class TailorRequest
{
    /// <summary>Generous cap (~3-4k words) that still bounds per-run Gemini cost/latency.</summary>
    public const int MaxFieldLength = 20_000;

    public string Resume { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
}
