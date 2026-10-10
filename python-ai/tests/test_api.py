from types import SimpleNamespace

from fastapi.testclient import TestClient

import agent_service
import main


class FakeAgent:
    def invoke(self, _input):
        return {"messages": [SimpleNamespace(content="- P&L totals $12,345 across the filtered positions.")]}


def test_health():
    response = TestClient(main.app).get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_ai_summary_accepts_dotnet_camel_case_payload(monkeypatch):
    monkeypatch.setattr(main, "get_open_ai_model", lambda: object())
    monkeypatch.setattr(agent_service, "_get_agent", lambda: FakeAgent())
    payload = {
        "rowCount": 18,
        "totalPnl": 12345.0,
        "totalFxExposure": -67890.0,
        "groups": [
            {"key": "Copper", "count": 18, "pnl": 12345.0, "fxExposure": -67890.0, "position": 55.0}
        ],
    }

    response = TestClient(main.app).post("/api/ai/risk-summary", json=payload)

    assert response.status_code == 200
    assert response.json() == {
        "summary": "- P&L totals $12,345 across the filtered positions.",
        "sourceRowCount": 18,
    }
