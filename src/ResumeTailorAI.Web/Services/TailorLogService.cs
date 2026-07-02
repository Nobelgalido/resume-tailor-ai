using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ResumeTailorAI.Web.Data;
using ResumeTailorAI.Web.Models;

namespace ResumeTailorAI.Web.Services;

/// <summary>
/// Records anonymous usage. Uses <see cref="IDbContextFactory{TContext}"/> rather than an
/// injected DbContext because a Blazor Server circuit outlives the request scope, so a
/// short-lived, per-operation context is the safe pattern.
/// </summary>
public class TailorLogService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public TailorLogService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task RecordAsync(TailorRequest request, TailorResult result, string? clientIp, CancellationToken ct = default)
    {
        await using var db = _factory.CreateDbContext();

        db.TailorLogs.Add(new TailorLog
        {
            ResumeLength = request.Resume?.Length ?? 0,
            JobDescriptionLength = request.JobDescription?.Length ?? 0,
            Model = result.Model,
            DurationMs = result.DurationMs,
            Success = result.Success,
            ClientHash = HashIp(clientIp)
        });

        await db.SaveChangesAsync(ct);
    }

    // One-way, truncated hash — enough to spot abuse patterns, not enough to identify anyone.
    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes)[..16];
    }
}
