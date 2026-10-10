from fastapi import FastAPI, HTTPException
from fastapi.concurrency import run_in_threadpool

from agent_service import generate_summary
from models import get_open_ai_model
from schemas import AiRiskSummaryResponse, RiskPositionSummary

app = FastAPI(
    title="CRR AI Summary API",
    version="1.0.0",
    description="Generates grounded narrative summaries from .NET risk-position aggregates.",
)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/ai/risk-summary", response_model=AiRiskSummaryResponse)
async def create_risk_summary(payload: RiskPositionSummary) -> AiRiskSummaryResponse:
    try:
        # Validate credentials before entering the worker thread for a clear configuration response.
        get_open_ai_model()
        summary = await run_in_threadpool(generate_summary, payload)
        return AiRiskSummaryResponse(summary=summary, source_row_count=payload.row_count)
    except RuntimeError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=502, detail="The AI provider could not generate a summary.") from exc
