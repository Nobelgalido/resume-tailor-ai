using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ResumeTailorAI.Web.Components;
using ResumeTailorAI.Web.Data;
using ResumeTailorAI.Web.Endpoints;
using ResumeTailorAI.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Respect a platform-injected PORT (Render, Railway, Heroku-style) and
// fall back to the container default of 8080 otherwise.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Trust the platform's reverse proxy so we see the real client scheme and IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Blazor (interactive server components).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Persistence. SQLite by default so it deploys anywhere with zero setup;
// flip Database:Provider to "SqlServer" (+ a connection string) for MSSQL.
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=resumetailor.db";
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(connectionString);
    else
        options.UseSqlite(connectionString);
});

// AI provider + the tailoring pipeline.
builder.Services.AddHttpClient<IChatCompletionService, GeminiChatCompletionService>();
builder.Services.AddScoped<IResumeTailorPipeline, ResumeTailorPipeline>();
builder.Services.AddScoped<TailorLogService>();

// Cap the public API at 10 requests/minute per client IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("tailor", context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// Create the database on first run (fine at demo scale; use migrations for MSSQL in prod).
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    // TLS is terminated at the edge in the cloud, so only redirect locally.
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseRateLimiter();

app.MapTailorEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
