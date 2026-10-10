# Corporate Risk Reporting (CRR)

A full-stack risk-position reporting slice built with .NET 10, React, TypeScript, AG Grid Community, and a separate Python AI summary API. It serves the supplied 50,000-row dataset through a server-paginated API and reports filtered totals across the complete matching result set.

## Requirements

- .NET 10 SDK
- Node.js 20+ and npm
- Python 3.11+

## Run locally

Open three terminals from the repository root.

**Terminal 1 — API**

```powershell
dotnet run --project backend/Crr.Api
```

The API listens on `http://localhost:5080`. Check `http://localhost:5080/api/health` for a health response.
Interactive API documentation is available at `http://localhost:5080/swagger` (OpenAPI JSON: `/swagger/v1/swagger.json`).

If Swagger displays **Failed to fetch** and the browser reports `ERR_CONNECTION_REFUSED`, the API is not listening on port 5080. Start the backend and keep that terminal running while using Swagger. This is a server availability issue, not CORS; Swagger UI and the API share the same origin. If the API is running but the React app cannot call it, verify it was launched on port 5080 and that the frontend uses `http://localhost:5173` or `http://127.0.0.1:5173` (the configured development CORS origins).

**Terminal 2 — frontend**

```powershell
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`.

**Terminal 3 — Python AI summary API**

```powershell
cd python-ai
py -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt
Copy-Item .env.example .env
```

Set `GROQ_API_KEY` in `python-ai/.env` (do not commit that file), then start the AI API:

```powershell
uvicorn main:app --reload --host 127.0.0.1 --port 8000
```

The Python API health check is `http://localhost:8000/health`; its interactive docs are at `http://localhost:8000/docs`. The .NET API calls `POST /api/ai/risk-summary` on the Python service. Override its base URL with the .NET configuration key `PythonAi:BaseUrl` (environment variable `PythonAi__BaseUrl`) if needed.

On Windows PowerShell, script activation may be restricted by execution policy. In that case use `.venv\Scripts\python.exe -m pip install -r requirements.txt`, then `.venv\Scripts\python.exe -m uvicorn main:app --reload --host 127.0.0.1 --port 8000` without activating the environment.

Run the Python service tests with `.venv\Scripts\python.exe -m pip install -r requirements-dev.txt` followed by `.venv\Scripts\python.exe -m pytest -q` from `python-ai`.

The API project links the root `risk_positions.csv` into its build output automatically. No separate database or seed command is required; the source file contains 50,000 data rows. The API reads it once at startup into memory.

## Features

- AG Grid Community Infinite Row Model with 100-row server-backed blocks and bounded cache.
- Server-side sorting and column filters for text, numeric values and dates. Free-text search covers trade ID, counterparty and commodity.
- P&L and FX totals, row counts and group summaries are calculated over all rows matching the active filters, not just the visible grid block.
- Commodity and book breakdowns; selecting a group applies that group as a grid filter. Commodity grouping includes signed position totals. Book grouping omits position sums because a book may contain commodities with unlike quantity units.
- Select a grid row to inspect its trade details.
- Export downloads a server-generated CSV containing all rows matching the active filters and search.
- **AI summary** requests an evidence-grounded narrative for the complete active filter set. The browser calls .NET only; .NET computes `/api/risk-positions/summary` and forwards those aggregates to the separate Python LangChain agent. Groq credentials stay in the Python service.

## API

- `GET /api/risk-positions?startRow=0&endRow=100&sortModel=[]&filterModel={}&q=` — paginated rows and filtered count.
- `GET /api/risk-positions/summary?groupBy=commodity&filterModel={}&q=` — filtered P&L / FX totals and full-result grouping.
- `GET /api/risk-positions/export?sortModel=[]&filterModel={}&q=` — full-result CSV download for the matching filters.
- `GET /api/risk-positions/summary/ai?groupBy=commodity&filterModel={}&q=` — .NET-to-Python narrative summary for matching aggregates.
- `GET /api/health` — readiness check.

Filter and sort models use AG Grid's JSON model format. The API applies only whitelisted field filters and sort keys.

## Project layout

- `backend/Crr.Api/Controllers` — HTTP route and response handling.
- `backend/Crr.Api/Services` — filtering, sorting, pagination, aggregation and CSV export logic.
- `backend/Crr.Api/Repositories` — CSV-backed in-memory data access.
- `backend/Crr.Api/Models` — position entity and API response models.
- `frontend` — React + TypeScript app and AG Grid Community infinite grid.
- `python-ai` — FastAPI service and LangChain agent for AI summaries; configure credentials in its local `.env`.
- `risk_positions.csv` — provided synthetic dataset (50,000 rows).
- `DESIGN.md` — architecture and tradeoffs.
