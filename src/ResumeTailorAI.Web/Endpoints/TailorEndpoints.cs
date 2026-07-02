using ResumeTailorAI.Web.Models;
using ResumeTailorAI.Web.Services;

namespace ResumeTailorAI.Web.Endpoints;

public static class TailorEndpoints
{
    /// <summary>
    /// POST /api/tailor — the same pipeline the UI uses, exposed as a plain JSON API.
    /// Rate limited to 10 requests/minute per IP.
    /// </summary>
    public static void MapTailorEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tailor", async (
            TailorRequest request,
            IResumeTailorPipeline pipeline,
            TailorLogService logService,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Resume) || string.IsNullOrWhiteSpace(request.JobDescription))
                return Results.BadRequest(new { error = "Both 'resume' and 'jobDescription' are required." });

            var stages = pipeline.BuildStages().ToList();
            var result = await pipeline.RunAsync(request, stages, onProgress: null, ct);

            await logService.RecordAsync(request, result, http.Connection.RemoteIpAddress?.ToString(), ct);

            return result.Success
                ? Results.Ok(result)
                : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
        })
        .RequireRateLimiting("tailor")
        .WithName("TailorResume");
    }
}
