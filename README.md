# AI-Augmented Code Review System

Starter implementation for the MSc research proposal *AI-Augmented Code
Review Systems* (S. F. Safna, K2635683). Combines GitHub pull request data,
SonarQube static analysis, and OpenAI GPT-4o reasoning into a single,
requirement-aware review comment posted back to each pull request.

Three guides, read in this order:

1. **[`IMPLEMENTATION_GUIDE.md`](./IMPLEMENTATION_GUIDE.md)** — step-by-step
   from installing prerequisites through to running your evaluation.
2. **[`TESTING_GUIDE.md`](./TESTING_GUIDE.md)** — a layered checklist for
   confirming each part actually works, from unit tests up to a live PR.
3. **[`DEMO_SCRIPT.md`](./DEMO_SCRIPT.md)** — a runbook for presenting the
   working prototype, including a reliable fallback path if a live API call
   misbehaves mid-demo.

## Layout

- `backend/` — ASP.NET Core 8 Web API (`CodeReview.Api`), the pure-logic
  domain layer (`CodeReview.Core`), and xUnit tests (`CodeReview.Tests`).
- `frontend/` — React + TypeScript dashboard (Vite).
- `docker-compose.yml` — Postgres + SonarQube + the API, for local dev.
- `.github/workflows/ci.yml` — build/test on every PR.
- `sonar-project.properties` — SonarQube scanner configuration.
- `scripts/` — trigger a review run directly (bypasses GitHub webhook
  delivery/ngrok), useful for both testing and demos, plus the Postgres init
  script that gives SonarQube its own database.

## API endpoints

| Endpoint | Purpose |
|---|---|
| `POST /api/webhook/github` | GitHub webhook receiver (HMAC-verified, runs in background) |
| `POST /api/reviews/run` | Run a review on demand — no webhook, no ngrok. Body: `{ "owner": "...", "repository": "...", "pullRequestNumber": 1 }`. Runs synchronously and returns the report |
| `GET /api/reviews` | Recent reviews, consumed by the dashboard |
| `GET /api/reviews/{id}` | A single review |
| `GET /api/reviews/export.csv` | All reviews as CSV, one row per finding — the evaluation data set for objective 6 |

## Quickest possible start

```bash
cd backend && dotnet restore && dotnet test   # verify the core logic first
cp .env.example .env                          # then fill in real credentials
docker compose up -d postgres sonarqube
cd frontend && npm install && npm run dev
```

Full detail, including GitHub webhook setup and the evaluation methodology
for objectives 4–6 of the proposal, is in `IMPLEMENTATION_GUIDE.md`.
