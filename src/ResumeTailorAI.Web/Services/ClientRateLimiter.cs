using System.Threading.RateLimiting;

namespace ResumeTailorAI.Web.Services;

/// <summary>
/// Shared per-client cap on tailoring runs, enforced identically whether the caller comes
/// through the Blazor UI or the JSON API — a single limiter instance so budget is shared
/// across both surfaces instead of only guarding the API a user could just... not use.
/// </summary>
public class ClientRateLimiter
{
    private readonly PartitionedRateLimiter<string> _limiter =
        PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    public bool TryAcquire(string? clientKey)
    {
        var key = string.IsNullOrWhiteSpace(clientKey) ? "unknown" : clientKey;
        using var lease = _limiter.AttemptAcquire(key);
        return lease.IsAcquired;
    }
}
