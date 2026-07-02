using ResumeTailorAI.Web.Models;

namespace ResumeTailorAI.Web.Services;

public interface IResumeTailorPipeline
{
    /// <summary>The ordered set of agents this pipeline will run, in their initial (pending) state.</summary>
    IReadOnlyList<PipelineStage> BuildStages();

    /// <summary>
    /// Runs the three agents in sequence, mutating <paramref name="stages"/> as it goes and
    /// invoking <paramref name="onProgress"/> after each state change so a UI can re-render live.
    /// </summary>
    Task<TailorResult> RunAsync(
        TailorRequest request,
        IList<PipelineStage> stages,
        Func<Task>? onProgress = null,
        CancellationToken ct = default);
}
