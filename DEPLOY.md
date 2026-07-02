# Deploying Resume Tailor · AI

You need two things first:

1. A **GitHub repo** with this code pushed to it.
2. A **Gemini API key** (free from [Google AI Studio](https://aistudio.google.com/app/apikey)).

---

## Step 0 — Push to GitHub

From the repo root:

```bash
git init
git add .
git commit -m "Resume Tailor AI — ASP.NET Core + Blazor rebuild"
git branch -M main
git remote add origin https://github.com/Nobelgalido/resume-tailor-ai.git
git push -u origin main
```

(Create the empty `resume-tailor-ai` repo on GitHub first, or use `gh repo create`.)

The `.gitignore` already excludes `bin/`, `obj/`, `*.db`, and any secrets — your API key is
**not** in the committed `appsettings.json` (it's blank there on purpose).

---

## Option A — Render (recommended: free, fastest to a live URL)

Render's free tier is aimed squarely at portfolio projects. No credit card required. The one
caveat: a free service **spins down after 15 minutes of inactivity** and takes ~1 minute to
wake on the next visit. Fine for a portfolio link; mention it or upgrade to the $7/mo Starter
plan (always-on) if you want instant loads for recruiters.

1. Go to **[dashboard.render.com](https://dashboard.render.com)** → **New** → **Web Service**.
2. Connect your GitHub and pick the `resume-tailor-ai` repo.
3. Render detects the `Dockerfile`. Confirm **Runtime: Docker**.
4. Choose the **Free** instance type.
5. Under **Environment**, add:
   - `Gemini__ApiKey` = *your key*
   - `Gemini__Model` = `gemini-2.5-flash` (optional; this is the default)
6. Click **Create Web Service**. First build takes a few minutes; you'll get a
   `https://resume-tailor-ai.onrender.com` URL.

**Note on the database:** Render's free filesystem is ephemeral, so the SQLite request-log
resets whenever the service redeploys or wakes. That's harmless here — nothing important lives
in it. If you ever want the log to persist, add a free Render Postgres (or point
`Database__Provider=SqlServer` at a hosted MSSQL) and set `ConnectionStrings__Default`.

Blazor Server runs over WebSockets; Render supports these on the free tier with no extra config.

---

## Option B — Azure App Service (best résumé optics for .NET roles)

Azure is the natural home for ASP.NET Core and looks strong on a .NET-focused CV. The free
**F1** tier can host it, but it has no "Always On" and tight limits; the **B1** Basic tier
(~US$13/mo) is the comfortable choice. Either way, Blazor Server needs two switches flipped.

Using the Azure CLI:

```bash
# 1. Sign in and create a resource group
az login
az group create --name resume-tailor-rg --location southeastasia

# 2. App Service plan (B1) + a Linux web app running the .NET 8 runtime
az appservice plan create --name resume-tailor-plan \
  --resource-group resume-tailor-rg --sku B1 --is-linux

az webapp create --name resume-tailor-galido \
  --resource-group resume-tailor-rg --plan resume-tailor-plan \
  --runtime "DOTNETCORE:8.0"

# 3. Required for Blazor Server: WebSockets ON + session (ARR) affinity ON
az webapp config set --name resume-tailor-galido \
  --resource-group resume-tailor-rg --web-sockets-enabled true

az webapp update --name resume-tailor-galido \
  --resource-group resume-tailor-rg --client-affinity-enabled true

# 4. App settings (your key)
az webapp config appsettings set --name resume-tailor-galido \
  --resource-group resume-tailor-rg \
  --settings Gemini__ApiKey="YOUR_KEY" Gemini__Model="gemini-2.5-flash"

# 5. Deploy the code
az webapp up --name resume-tailor-galido \
  --resource-group resume-tailor-rg --runtime "DOTNETCORE:8.0"
```

Your app lands at `https://resume-tailor-galido.azurewebsites.net`.

> Both settings in step 3 are non-negotiable for Blazor Server: **WebSockets** lets the SignalR
> circuit connect, and **session affinity** keeps a user pinned to the same instance so the
> circuit survives.

You can also deploy the Docker image to **Azure Container Apps** instead of App Service; the
same two requirements apply (enable session affinity on the container app).

---

## After it's live — add it to your portfolio

- Link the live URL and the GitHub repo from the projects section.
- One-liner that sells the engineering, not just the demo:
  *"Three-agent resume-tailoring pipeline on ASP.NET Core 8 + Blazor, with EF Core persistence,
  built-in rate limiting, and a provider-agnostic AI layer — Dockerized and deployed."*
- If you're on Render free, either note the ~1-min cold start or spend the $7/mo so a recruiter's
  first click loads instantly.

## Troubleshooting

- **"Gemini API key is not configured"** — the `Gemini__ApiKey` env var didn't get set on the host.
- **App loads but shows "Attempting to reconnect"** — WebSockets/affinity not enabled (Azure), or
  the free service just woke from spin-down (Render); refresh once it's up.
- **429 responses** — the rate limiter (10/min/IP) kicked in; expected under rapid retries.
- **Build fails restoring packages** — confirm the host has network access to nuget.org (all
  managed platforms do; a locked-down corporate network may not).
