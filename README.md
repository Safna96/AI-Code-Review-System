# AI-Augmented Code Review System

Implementation of the MSc research proposal *AI-Augmented Code Review
Systems* (S. F. Safna, K2635683). Combines GitHub pull request data,
SonarQube static analysis, and LLM reasoning into a single,
requirement-aware review comment posted back to each pull request.

## A note on the model

The proposal specifies GPT-4o. The system is written against the OpenAI SDK
and still runs GPT-4o unchanged, but the endpoint and model are configuration
(`OpenAI:BaseUrl`, `OpenAI:Model`), so it also runs against any
OpenAI-compatible provider. Development and the runs recorded here used
Google's **gemini-3.5-flash** via Gemini's OpenAI compatibility layer, whose
free tier removed the cost barrier to the repeated runs that developing and
evaluating the pipeline required.

Because the model is configuration rather than a fixed dependency, **every
stored review records the model that produced it** (`ModelName`, included in
`GET /api/reviews` and the CSV export). Results from different models are
therefore separable after the fact rather than silently blended.

Two practical notes for anyone reproducing this:

- Model names expire. `gemini-2.0-flash` and `gemini-2.5-flash-lite` were both
  already withdrawn during development and returned HTTP 404. Pin a model and
  record it with each batch of results.
- Free-tier endpoints return HTTP 503 under load frequently enough that a
  single attempt usually fails; the retry logic in `OpenAiReviewService` is
  required for the pipeline to work at all, not a defensive nicety.

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
