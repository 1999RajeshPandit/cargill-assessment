import json
from functools import lru_cache

from langchain.agents import create_agent

from models import get_open_ai_model
from schemas import RiskPositionSummary

SYSTEM_PROMPT = """You are an assistant for corporate commodity risk managers.
Give one concise, practical REVIEW  and SUGGESTION  based on the supplied risk aggregates.
Do not repeat numeric data, which is already shown in the dashboard. Avoid unsupported
causes or trends and trading recommendations. If no rows match, say no suggestion is
available.
"""


@lru_cache(maxsize=1)
def _get_agent():
    return create_agent(
        model=get_open_ai_model(),
        tools=[],
        name="crr_risk_summary_agent",
    )


def generate_summary(summary: RiskPositionSummary) -> str:
    compact_summary = {
        "matching_rows": summary.row_count,
        "total_pnl_usd": summary.total_pnl,
        "total_fx_exposure_usd": summary.total_fx_exposure,
        "groups": [
            {
                "group": group.key,
                "trades": group.count,
                "pnl_usd": group.pnl,
                "fx_exposure_usd": group.fx_exposure,
                "position": group.position,
            }
            for group in summary.groups[:20]
        ],
    }
    result = _get_agent().invoke(
        {
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {
                    "role": "user",
                    "content": "Summarize this filtered risk aggregation:\n"
                    + json.dumps(compact_summary, separators=(",", ":")),
                },
            ]
        }
    )
    content = result["messages"][-1].content
    if isinstance(content, str):
        return content.strip()
    if isinstance(content, list):
        return "\n".join(
            str(part.get("text", "")) for part in content if isinstance(part, dict)
        ).strip()
    return str(content).strip()
