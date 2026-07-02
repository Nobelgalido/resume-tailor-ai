namespace ResumeTailorAI.Web.Data;

/// <summary>
/// An anonymous record of one tailoring run. Deliberately stores no resume or job text —
/// only lengths, timing, and a truncated hash of the client IP for rate-limit auditing.
/// </summary>
public class TailorLog
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public int ResumeLength { get; set; }
    public int JobDescriptionLength { get; set; }

    public string Model { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public bool Success { get; set; }

    public string? ClientHash { get; set; }
}
