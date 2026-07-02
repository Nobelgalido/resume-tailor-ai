# Resume Tailor · AI

A three-agent AI pipeline that rewrites a resume to target any job description — built on
**ASP.NET Core 8**, **Blazor**, and **EF Core**, with **Gemini** doing the language work.

Paste a resume and a job posting; three agents run in sequence and hand off to each other:

1. **Analyze** — extracts must-haves, nice-to-haves, and ATS keywords from the job description.
2. **Strategize** — maps the resume against that analysis; surfaces strong matches and honest gaps.
3. **Rewrite** — produces a tailored, ATS-friendly resume grounded in real experience (it is
   explicitly instructed never to invent employers, titles, dates, or skills).

The pipeline's progress is shown live in the UI, and each agent's intermediate reasoning is
available under a disclosure so the output is never a black box.

You can paste your resume as plain text/markdown, or **upload a PDF** and have the text
extracted automatically into the editable textarea. The tailored result can be copied as
markdown or **downloaded as a formatted PDF** — a clean, single-column, ATS-style document
generated in memory (nothing is ever written to disk).

> This is the C#/.NET rebuild of an earlier Python + Streamlit version, redone in the ASP.NET
> ecosystem as a deployable portfolio piece.

## Tech stack

| Layer      | Choice |
|------------|--------|
| Web / UI   | ASP.NET Core 8, Blazor (interactive server components) |
| API        | Minimal API (`POST /api/tailor`) sharing the same pipeline |
| AI         | Gemini via `HttpClient` (no SDK) behind an `IChatCompletionService` interface |
| PDF        | **iText7** (upload text extraction + tailored-resume PDF export), **Markdig** (markdown parsing/rendering) |
| Data       | EF Core — SQLite by default, one config flip to SQL Server |
| Hardening  | Shared rate limiting (10 req/min/client, enforced identically for the UI and API), forwarded-headers, anonymous request logging |
| Packaging  | Multi-stage `Dockerfile` (deploys anywhere) |

> iText7 is used under its AGPL license, which is free for this public open-source project;
> a commercial license would only be required for closed-source use.

## Architecture

```
Browser ──▶ Blazor page (Home.razor)
                 │  injects
                 │  PDF upload ──▶ PdfResumeTextExtractor ──▶ resume textarea
                 ▼
         IResumeTailorPipeline ──▶ IChatCompletionService ──▶ Gemini REST
                 │                        (Gemini impl)
                 ├─ Agent 1: Analyze
                 ├─ Agent 2: Strategize
                 └─ Agent 3: Rewrite
                 │
                 ├─ tailored resume ──▶ MarkdownPdfRenderer ──▶ "Download PDF"
                 ▼
         TailorLogService ──▶ EF Core ──▶ SQLite / SQL Server
                 ▲
POST /api/tailor ┘  (same pipeline, rate limited)
```

The AI provider sits behind `IChatCompletionService`, so swapping Gemini for Claude or OpenAI
is a new implementation and one line in `Program.cs` — the pipeline never changes.

Both PDF features operate purely at the UI edges — `TailorRequest`, `TailorResult`,
`IResumeTailorPipeline`, and `/api/tailor` are untouched by them, and no PDF ever touches
disk (extraction and rendering both run entirely in memory).

If Gemini responds with a rate-limit error (HTTP 429), the pipeline surfaces a specific,
stage-tagged message (e.g. *"Rate limit hit during the 'Rewrite' step…"*) instead of a raw
API error, via a dedicated `GeminiRateLimitException`.

## Run it locally

Requires the **.NET 8 SDK** and a **Gemini API key** (free from Google AI Studio).

```bash
# from the repo root
dotnet restore

# set your key (don't commit it) — user-secrets is cleanest for local dev
cd src/ResumeTailorAI.Web
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY_HERE"

dotnet run
```

Then open the URL it prints (e.g. `https://localhost:5001`).

Prefer an environment variable instead of user-secrets? Use the double-underscore form:

```bash
export Gemini__ApiKey="YOUR_KEY_HERE"
dotnet run
```

## Configuration

| Key | Env var | Default | Notes |
|-----|---------|---------|-------|
| `Gemini:ApiKey` | `Gemini__ApiKey` | *(empty)* | Required. Never commit it. |
| `Gemini:Model` | `Gemini__Model` | `gemini-2.5-flash` | Any current Gemini model id. |
| `Database:Provider` | `Database__Provider` | `Sqlite` | Set to `SqlServer` for MSSQL. |
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | `Data Source=resumetailor.db` | Your DB connection string. |

### Switching to SQL Server

```bash
export Database__Provider="SqlServer"
export ConnectionStrings__Default="Server=localhost;Database=ResumeTailor;Trusted_Connection=True;TrustServerCertificate=True;"
```

The app calls `EnsureCreated()` on startup, which is fine at this scale. For a real MSSQL
deployment you'd switch to EF migrations (`dotnet ef migrations add Initial`).

## The API

```bash
curl -X POST https://your-app.example.com/api/tailor \
  -H "Content-Type: application/json" \
  -d '{ "resume": "…your resume text…", "jobDescription": "…the posting…" }'
```

Returns the tailored resume plus both intermediate agent outputs, model name, and timing.
`resume` and `jobDescription` are each capped at 20,000 characters, and requests are rate
limited to 10 per minute per client (same shared budget as the Blazor UI).

## Privacy

Resume and job-description text are never stored. The only thing written to the database is an
anonymous row per run: text *lengths*, the model used, duration, success, and a truncated
one-way hash of the client IP (for spotting abuse). See `Data/TailorLog.cs`.

## Deploying

See **[DEPLOY.md](DEPLOY.md)** for step-by-step instructions (Render free tier and Azure App Service).

---

Built by [Alfred Nobel Galido](https://alfrednobelgalido.vercel.app) ·
[github.com/Nobelgalido](https://github.com/Nobelgalido)
