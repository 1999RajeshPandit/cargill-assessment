from pydantic import BaseModel, ConfigDict, Field


class RiskPositionGroup(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    key: str
    count: int = Field(ge=0)
    pnl: float
    fx_exposure: float = Field(alias="fxExposure")
    position: float | None = None


class RiskPositionSummary(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    row_count: int = Field(alias="rowCount", ge=0)
    total_pnl: float = Field(alias="totalPnl")
    total_fx_exposure: float = Field(alias="totalFxExposure")
    groups: list[RiskPositionGroup]


class AiRiskSummaryResponse(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    summary: str
    source_row_count: int = Field(alias="sourceRowCount")
