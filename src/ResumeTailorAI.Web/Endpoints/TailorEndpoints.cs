using ResumeTailorAI.Web.Models;
using ResumeTailorAI.Web.Services;

namespace ResumeTailorAI.Web.Endpoints;

public static class TailorEndpoints
{
    /// <summary>
    /// POST /api/tailor — the same pipeline the UI uses, exposed as a plain JSON API.
    /// Rate limited to 10 requests/minute per IP (shared budget with the Blazor UI).
    /// </summary>
    public static void MapTailorEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tailor", async (
            TailorRequest request,
            IResumeTailorPipeline pipeline,
            TailorLogService logService,
            ClientRateLimiter rateLimiter,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Resume) || string.IsNullOrWhiteSpace(request.JobDescription))
                return Results.BadRequest(new { error = "Both 'resume' and 'jobDescription' are required." });

            if (request.Resume.Length > TailorRequest.MaxFieldLength || request.JobDescription.Length > TailorRequest.MaxFieldLength)
                return Results.BadRequest(new { error = $"'resume' and 'jobDescription' must each be under {TailorRequest.MaxFieldLength:N0} characters." });

            var clientIp = http.Connection.RemoteIpAddress?.ToString();
            if (!rateLimiter.TryAcquire(clientIp))
                return Results.Problem(detail: "Rate limit exceeded — try again in a minute.", statusCode: StatusCodes.Status429TooManyRequests);

            var stages = pipeline.BuildStages().ToList();
            var result = await pipeline.RunAsync(request, stages, onProgress: null, ct);

            await logService.RecordAsync(request, result, clientIp, ct);

            return result.Success
                ? Results.Ok(result)
                : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
        })
        .WithName("TailorResume");
    }
}
