# Design notes

## Data and API

The API stores the 50,000 supplied risk positions in PostgreSQL through EF Core and Npgsql. `TradeId` is the primary key; numeric values and valuation dates use PostgreSQL numeric/date types. Indexes support common book, commodity, counterparty, region, and valuation-date access. Filtering, sorting, pagination, and summary aggregation execute in SQL, with request cancellation passed to async database operations and stable trade ID tie-breaking for pages.

The CSV is loaded explicitly after applying the EF migration. The importer validates every row, rejects repeated/existing trade IDs, and uses PostgreSQL binary `COPY` in one transaction so malformed input cannot leave a partial import. Credentials are stored in .NET user-secrets or environment variables, not in the repository. This keeps API startup independent of the source CSV while making the initial dataset load repeatable against a clean database.

## Frontend

The React client uses AG Grid Community's Infinite Row Model. It requests 100-row blocks and limits cached blocks, while sorting and per-column filters are serialized to the API. The portfolio summary is requested independently and recomputed for the full active filter set. Because free Community AG Grid does not include row grouping or pivoting, a lightweight API-backed exposure breakdown provides commodity/book grouping. Commodity groups can be selected to filter the position grid. Position quantity is summed only in commodity groups; book-level position totals are intentionally omitted because mixed commodity units are not directly comparable. Selecting a row reveals a trade-level drilldown.

## Search and future scale

Quick search is a case-insensitive substring match over trade ID, counterparty and commodity. PostgreSQL handles the current scale as the system of record. For substantially larger datasets, substring search may need a trigram index or a dedicated search service; deep pagination may also need a cursor-based API rather than offsets.

## Improvements with more time

Add API and UI automated tests (including filter/summary/export parity), persist user column layouts, and wire a CI workflow for build/test. For larger datasets, stream the server-generated export rather than buffering the complete CSV in memory.

## AI narrative summary

The optional AI feature is a separate FastAPI project in `python-ai`. The browser calls only the .NET `GET /api/risk-positions/summary/ai` endpoint. .NET applies the requested filters and computes the authoritative summary, then posts the aggregates (not individual trade rows) to Python. Python validates the camelCase payload, invokes a LangChain agent backed by Groq, and returns a short narrative. `GROQ_API_KEY` and optional LangSmith tracing credentials remain in the Python service's untracked `.env`; no model credentials are sent to the browser. The prompt asks the model to stick to supplied figures and avoid combining quantities across different commodities. A configured model-provider credential is required for live AI responses; health and test routes work without it.

To enable live AI responses, create a Groq API key at [console.groq.com/keys](https://console.groq.com/keys) and set it as `GROQ_API_KEY` in `python-ai/.env`.
