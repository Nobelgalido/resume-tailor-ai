namespace ResumeTailorAI.Web.Models;

public enum StageStatus
{
    Pending,
    Running,
    Complete,
    Failed
}

/// <summary>One agent in the tailoring pipeline. Mutated as the run progresses so the UI can reflect live state.</summary>
public class PipelineStage
{
    public int Number { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public StageStatus Status { get; set; } = StageStatus.Pending;
    public string? Output { get; set; }
}
