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
        catch (GeminiRateLimitException ex)
        {
            _logger.LogWarning(ex, "Resume tailoring pipeline hit the Gemini rate limit");

            var running = stages.FirstOrDefault(s => s.Status == StageStatus.Running);
            if (running is not null) running.Status = StageStatus.Failed;

            result.Success = false;
            result.Error = running is not null
                ? $"Rate limit hit during the \"{running.Name}\" step — the Gemini API quota for this key has been used up. Wait a bit and try again."
                : "Rate limit hit — the Gemini API quota for this key has been used up. Wait a bit and try again.";
            await Notify();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume tailoring pipeline failed");

            var running = stages.FirstOrDefault(s => s.Status == StageStatus.Running);
            if (running is not null) running.Status = StageStatus.Failed;

            result.Success = false;
            result.Error = running is not null
                ? $"The \"{running.Name}\" step failed: {ex.Message}"
                : ex.Message;
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
        You are an expert resume writer specializing in ATS (Applicant Tracking System)
        optimization. Tailor the candidate's resume for the target job, strictly following
        these rules, using the job analysis and strategy provided.

        FORMAT & LAYOUT
        - Single-column, standard layout only. No graphics, columns, text boxes, headers,
          footers, tables, or profile photos — these confuse ATS parsers, which read
          top-to-bottom, left-to-right.
        - Reverse-chronological order: most recent roles and education first.
        - Use exactly these section headings, in ALL CAPS, in this order, and nothing
          else: PROFESSIONAL SUMMARY, SKILLS, WORK EXPERIENCE, EDUCATION. Insert a
          RELEVANT PROJECTS section after Work Experience, and/or a CERTIFICATIONS
          section after Education, only if the candidate's original resume actually
          has projects or certifications worth including. Do not rename, merge, or
          get creative with these headings.

        RESUME STRUCTURE (as markdown)
        1. Header:
           - Line 1: the candidate's full name as a level-1 heading, e.g. `# Jane Doe`.
           - Line 2: a single plain paragraph (not a heading) with every contact detail
             the candidate actually provided, separated by " | ", in this order where
             available: location, phone, email, LinkedIn, GitHub, portfolio/website.
             Never invent a contact detail that isn't in the original resume.
             Make email, LinkedIn, GitHub, and portfolio links clickable using markdown
             link syntax — human-readable label, real URL as the target — for example
             `[jane@email.com](mailto:jane@email.com)` and
             `[linkedin.com/in/janedoe](https://linkedin.com/in/janedoe)`.
             Put line 1 and line 2 immediately adjacent with NO blank line between them,
             so they stay visually tight together as one compact header block.
        2. PROFESSIONAL SUMMARY — a 2-3 sentence paragraph highlighting the candidate's
           experience and top skills relevant to this role.
        3. SKILLS — a bulleted list of 5-10 skills grouped by category, matched
           directly to keywords in the job posting where the candidate genuinely has
           them. Format each bullet as `**Category:** skill, skill, skill`.
        4. WORK EXPERIENCE — for each role, in this exact pattern:
           - A bold line with just the job title, e.g. `**Job Title**`.
           - Directly on the next line, with NO blank line in between, an italic line
             with company, location, and dates, e.g.
             `*Company Name — Location | Month Year – Month Year*`.
           - Bullet points using strong action verbs, leading with quantifiable impact
             (e.g. "reduced load time 20%", "$10k saved") rather than daily duties.
        5. RELEVANT PROJECTS (only if applicable) — same bold/italic pattern as Work
           Experience: bold project name, then directly on the next line (no blank
           line between) an italic tech-stack/link line, then bullets.
        6. EDUCATION — a bold line with the degree, then directly on the next line
           (no blank line between) an italic line with institution, location, and
           dates.
        7. CERTIFICATIONS (only if applicable) — a bulleted list of certification
           name, issuer, and year.

        Within every bold-title / italic-subtitle pair above (Header, each Work
        Experience entry, each Project, Education), the two lines must be part of the
        SAME paragraph — separated only by a single newline, never by a blank line —
        so they render as one tight visual unit rather than two separately-spaced
        blocks.

        CONTENT STRATEGY
        - Keywords: copy hard skills and terminology directly from the job posting into
          the resume wherever they truthfully apply, using the posting's own phrasing.
        - Acronyms: spell out each acronym and include the abbreviation alongside it on
          first use (e.g. "Search Engine Optimization (SEO)").
        - Reorder and reword real experience to foreground what the role values most.

        CONSTRAINTS
        - Do not fabricate employers, titles, dates, degrees, skills, or metrics —
          represent the candidate honestly. Only use what is in the original resume.
        - Keep it to a single page worth of content unless the candidate's experience
          clearly warrants more.
        - Preserve contact details exactly as given, including real URLs for any
          links, so they resolve correctly when rendered as clickable links.

        Output ONLY the finished resume in markdown as described above, with no
        commentary before or after.
        """;
}
