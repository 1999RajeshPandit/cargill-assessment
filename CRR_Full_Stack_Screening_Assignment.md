**Corporate Risk Reporting (CRR)**

**Full-Stack Screening Assignment**

*Role: Trading Engineer (Full Stack)* · Submit within: 1–3 calendar days

------------------------------------------------------------------------

# 1. Overview

Thanks for your interest in the role. This is a practical, time-boxed
exercise that mirrors the core of the job: building a fast, intuitive
reporting experience over a large risk dataset. We care far more about a
polished, well-reasoned core than a broad, unfinished submission — depth
beats breadth.

# 2. The scenario

The CRR platform lets internal risk managers explore enterprise risk
data — positions, P&L, and FX exposures — across trading books. Analysts
need to slice a large dataset quickly: filter, group, drill down, and
see aggregated totals without waiting. Your task is to build a small
vertical slice of that experience.

# 3. What you'll build

A single risk-reporting view backed by a .NET API and a React + AG Grid
frontend, served from a dataset of realistic risk positions. Users
should be able to browse, sort, filter, group, and drill into the data
and see aggregated totals — smoothly, even at scale.

# 4. Tech constraints

- **Backend:** .NET 10 / ASP.NET Core 10 Web API, C#.

- **Frontend:** React with TypeScript, using AG Grid for the data grid.

- **Data store:** PostgreSQL or an in-memory store is acceptable if you
  can't stand one up in the time, but explain the tradeoff in your
  design note.

- **Everything else** (styling, libraries, tooling) is your choice.

# 5. The dataset

Use the supplied [risk_positions.csv](risk_positions.csv), which contains
synthetic risk-position data. Your solution must serve at least 50,000 rows;
if you generate additional data, document the seeding step.

| **Field** | **Type** | **Example / notes** |
|----|----|----|
| **tradeId** | string | unique id, e.g. TRD-000123 |
| **book** | string | trading desk: Ferrous, Non-Ferrous, Freight, Ags |
| **commodity** | string | HRC Steel, Copper, Aluminium, Iron Ore,etc |
| **counterparty** | string | company name (free-text — used for search) |
| **region** | string | APAC, EMEA, AMER |
| **instrumentType** | string | Physical, Future, Swap |
| **position** | number | signed quantity (e.g. metric tons) |
| **currency** | string | USD, EUR, CNY, SGD |
| **marketPrice** | number | current mark price |
| **pnl** | number | signed P&L in USD |
| **fxExposure** | number | signed FX exposure in USD |
| **asOfDate** | date | valuation date |

# 6. Core requirements (required)

## Backend (.NET)

- REST endpoint(s) serving the risk data with server-side pagination,
  sorting, and filtering.

- At least one aggregation endpoint returning totals for **all rows matching
  the active filters** (not just the current page), such as sums of P&L,
  position, and FX exposure grouped by book or commodity. Position is a signed
  quantity, and units may differ by commodity; avoid implying that quantities
  across different commodities are directly comparable.

- Sensible layering and clean, readable structure.

## Frontend (React + TypeScript + AG Grid)

- With the free AG Grid Community edition, use the Infinite Row Model for
  server-backed loading, sorting, and filtering; implement grouping and
  aggregate summaries through your API (built-in row grouping and pivoting
  require Enterprise).

- Sorting and filtering wired through to the backend.

- Provide a book- or commodity-level summary/grouping view with aggregated
  totals from the API; explain your approach.

- A drilldown or master/detail view to inspect a group or an individual
  row.

## Performance

- The full dataset must load and scroll smoothly, with no UI freeze.

# 7. Stretch goals (optional — these differentiate)

- Free-text / fuzzy filtering across counterparty and commodity (the
  natural fit for OpenSearch).

- Back the API with OpenSearch / Elasticsearch — share your index design
  and query approach.

- Persist column state or saved views.

- A summary chart (e.g. P&L by book or region).

- Automated tests (FE + BE)

- A CI pipeline (GitHub Actions) that builds and tests both apps.

# 8. Deliverables

- A GitHub repo (or zip) containing the backend and frontend.

- A README with exact steps to run locally — commands, ports, and any
  data-seeding step.

- **A short DESIGN.md (½–1 page):** key decisions, how you handled the
  large dataset, and — if you didn't use OpenSearch — how you would.
  Note anything you'd improve with more time.

# 9. Time expectation

- Submit within **1–3 calendar days**. The task should take no more than
  **3–4 hours**; prioritize a clean, working core over breadth. Feel free to
  use AI tools. It's fine to leave stretch goals undone — briefly note what
  you would improve next and why in your design note.

# 10. Sharing your submission

- Share a link to your Git repository (GitHub or an equivalent service) when
  submitting the assessment, and make the repository public so the review
  team can access it.