using System.Diagnostics;
using ResumeTailorAI.Web.Models;

namespace ResumeTailorAI.Web.Services;

/// <summary>
/// Three agents run in sequence, each one's output feeding the next:
///   1. Analyze   — pull structured requirements out of the job description.
///   2. Strategize — map the resume against those requirements; find matches and gaps.
///   3. Rewrite    — produce a tailored, ATS-friendly resume grounded in real experience.
/// </summary>
public class ResumeTailorPipeline : IResumeTailorPipeline
{
    private readonly IChatCompletionService _ai;
    private readonly ILogger<ResumeTailorPipeline> _logger;

    public ResumeTailorPipeline(IChatCompletionService ai, ILogger<ResumeTailorPipeline> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public IReadOnlyList<PipelineStage> BuildStages() => new List<PipelineStage>
    {
        new() { Number = 1, Name = "Analyze",    Description = "Extracting requirements and keywords from the job description" },
        new() { Number = 2, Name = "Strategize", Description = "Mapping your experience to the role and surfacing gaps" },
        new() { Number = 3, Name = "Rewrite",    Description = "Rewriting your resume to target the role" }
    };

    public async Task<TailorResult> RunAsync(
        TailorRequest request,
        IList<PipelineStage> stages,
        Func<Task>? onProgress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new TailorResult { Model = _ai.Model };

        Task Notify() => onProgress?.Invoke() ?? Task.CompletedTask;

        try
        {
            // Stage 1 — analyze the job description.
            stages[0].Status = StageStatus.Running;
            await Notify();
            var analysis = await _ai.CompleteAsync(
                AnalyzePrompt,
                $"JOB DESCRIPTION:\n{request.JobDescription}",
                ct);
            stages[0].Output = analysis;
            stages[0].Status = StageStatus.Complete;
            result.JobAnalysis = analysis;
            await Notify();

            // Stage 2 — compare the resume against the analysis.
            stages[1].Status = StageStatus.Running;
            await Notify();
            var strategy = await _ai.CompleteAsync(
                StrategyPrompt,
                $"CANDIDATE RESUME:\n{request.Resume}\n\nJOB ANALYSIS:\n{analysis}",
                ct);
            stages[1].Output = strategy;
            stages[1].Status = StageStatus.Complete;
            result.MatchStrategy = strategy;
            await Notify();

            // Stage 3 — rewrite the resume for the role.
            stages[2].Status = StageStatus.Running;
            await Notify();
            var tailored = await _ai.CompleteAsync(
                RewritePrompt,
                $"ORIGINAL RESUME:\n{request.Resume}\n\nJOB ANALYSIS:\n{analysis}\n\nTAILORING STRATEGY:\n{strategy}",
                ct);
            stages[2].Output = tailored;
            stages[2].Status = StageStatus.Complete;
            result.TailoredResume = tailored;
            await Notify();

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume tailoring pipeline failed");

            var running = stages.FirstOrDefault(s => s.Status == StageStatus.Running);
            if (running is not null) running.Status = StageStatus.Failed;

            result.Success = false;
            result.Error = ex.Message;
            await Notify();
        }
        finally
        {
            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
        }

        return result;
    }

    private const string AnalyzePrompt = """
        You are a technical recruiter. Read the job description and extract a concise, structured analysis.
        Return markdown with these sections:
        - **Role summary** (1-2 sentences)
        - **Must-have skills** (bullet list of exact keywords / technologies)
        - **Nice-to-have skills** (bullet list)
        - **Seniority & focus** (what the role really cares about)
        - **ATS keywords** (a comma-separated list a resume should include)
        Be specific. Do not invent requirements that are not in the text.
        """;

    private const string StrategyPrompt = """
        You are a career coach helping a candidate target a specific role.
        Compare the candidate's resume against the job analysis and return markdown:
        - **Strong matches** (resume experience that directly maps to must-haves)
        - **Gaps** (must-haves the resume does not currently evidence)
        - **Reframing opportunities** (real experience to re-word using the role's language)
        - **Priority keywords to surface** (from the ATS list that are missing or buried)
        Be honest about gaps. Never suggest fabricating experience the candidate does not have.
        """;

    private const string RewritePrompt = """
        You are an expert resume writer. Rewrite the candidate's resume to target this role,
        using the analysis and strategy provided. Rules:
        - Keep every claim truthful. Do NOT invent employers, titles, dates, degrees, or skills.
        - Reorder and reword real experience to foreground what the role values.
        - Weave in relevant ATS keywords naturally, only where they genuinely apply.
        - Keep it ATS-friendly: clean markdown, standard section headings, no tables or columns.
        - Preserve contact details exactly as given.
        Output ONLY the finished resume in markdown, with no commentary before or after.
        """;
}
