# CRR AI Summary API

A standalone FastAPI service that turns .NET-generated risk-position aggregates into a concise, grounded narrative with a LangChain agent and Groq-hosted chat model.

## Local setup (Windows PowerShell)

From the repository root:

```powershell
py -m venv python-ai/.venv
python-ai/.venv/Scripts/python.exe -m pip install -r python-ai/requirements.txt
Copy-Item python-ai/.env.example python-ai/.env
```

Set `GROQ_API_KEY` in `python-ai/.env`, then run:

```powershell
Push-Location python-ai
.\.venv\Scripts\python.exe -m uvicorn main:app --reload --host 127.0.0.1 --port 8000
Pop-Location
```

Keep this process running while using the frontend. API docs are at `http://localhost:8000/docs`; health check is `http://localhost:8000/health`.

## Contract

`POST /api/ai/risk-summary` accepts the .NET aggregate JSON:

```json
{
  "rowCount": 42,
  "totalPnl": 125000.0,
  "totalFxExposure": -45000.0,
  "groups": [
    { "key": "Copper", "count": 42, "pnl": 125000.0, "fxExposure": -45000.0, "position": 750.0 }
  ]
}
```

The response has `summary` and `sourceRowCount`. Only aggregate values are sent; raw trades are not passed to the model. The .NET API calls this service, so the browser never receives or uses the model credentials.

## Configuration

- `GROQ_API_KEY` — required to generate a narrative.
- `GROQ_MODEL` — optional model name; defaults to `llama-3.3-70b-versatile`.
- `LANGSMITH_API_KEY`, `LANGCHAIN_ENDPOINT`, `LANGCHAIN_PROJECT`, `LANGCHAIN_TRACING_V2` — optional LangSmith tracing, enabled when a key is set.
- `HF_TOKEN` — optional for a future Hugging Face model-provider implementation; unused by the current Groq model.

## Tests

```powershell
python-ai/.venv/Scripts/python.exe -m pip install -r python-ai/requirements-dev.txt
Push-Location python-ai
.\.venv\Scripts\python.exe -m pytest -q
Pop-Location
```
