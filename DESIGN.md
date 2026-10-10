# Design notes

## Data and API

The API loads the provided 50,000-row CSV once at process startup and retains typed records in memory. This keeps the assessment self-contained and avoids database provisioning or seed drift; for this dataset size, memory-backed queries are simple and responsive. The CSV is linked into the API build output from the repository root. The API exposes a bounded pagination endpoint plus a separate summary endpoint. Each request applies the same AG Grid filter model (and quick-search term); summaries aggregate the complete filtered set before pagination, so headline totals never describe only the visible page. Sort fields are explicitly whitelisted, and page size is capped.

For production, I would move the canonical positions into PostgreSQL with typed columns and indexes on valuation date, book, commodity, region and trade ID. Query filtering, sorting, paging, and grouped sums would then execute in SQL, with a stable snapshot/version boundary so page and summary calls see consistent data. I would add request cancellation, query limits, telemetry, and integration tests against a disposable database.

## Frontend

The React client uses AG Grid Community's Infinite Row Model. It requests 100-row blocks and limits cached blocks, while sorting and per-column filters are serialized to the API. The portfolio summary is requested independently and recomputed for the full active filter set. Because free Community AG Grid does not include row grouping or pivoting, a lightweight API-backed exposure breakdown provides commodity/book grouping. Commodity groups can be selected to filter the position grid. Position quantity is summed only in commodity groups; book-level position totals are intentionally omitted because mixed commodity units are not directly comparable. Selecting a row reveals a trade-level drilldown.

## Search and future scale

Quick search is a case-insensitive substring match over trade ID, counterparty and commodity. At this scale an in-memory scan is adequate; production search across a much larger corpus could use OpenSearch with keyword fields for exact facets and normalized text fields for analyzed matching. I would index trade ID, book, commodity, counterparty, region, instrument type, currency and valuation date, with numeric position / P&L / FX fields. Filter facets and aggregations would be terms/date/numeric aggregations, and paged results would use point-in-time plus `search_after` rather than deep offsets. PostgreSQL would remain the system of record.

## Improvements with more time

Add API and UI automated tests (including filter/summary/export parity), persist user column layouts, and wire a CI workflow for build/test. For larger datasets, stream the server-generated export rather than buffering the complete CSV in memory.

## AI narrative summary

The optional AI feature is a separate FastAPI project in `python-ai`. The browser calls only the .NET `GET /api/risk-positions/summary/ai` endpoint. .NET applies the requested filters and computes the authoritative summary, then posts the aggregates (not individual trade rows) to Python. Python validates the camelCase payload, invokes a LangChain agent backed by Groq, and returns a short narrative. `GROQ_API_KEY` and optional LangSmith tracing credentials remain in the Python service's untracked `.env`; no model credentials are sent to the browser. The prompt asks the model to stick to supplied figures and avoid combining quantities across different commodities. A configured model-provider credential is required for live AI responses; health and test routes work without it.

To enable live AI responses, create a Groq API key at [console.groq.com/keys](https://console.groq.com/keys) and set it as `GROQ_API_KEY` in `python-ai/.env`.
