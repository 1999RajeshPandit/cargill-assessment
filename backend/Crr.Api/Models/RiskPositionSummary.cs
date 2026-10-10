namespace Crr.Api.Models;

public sealed record RiskPositionGroup(string Key, int Count, decimal Pnl, decimal FxExposure, decimal? Position);

public sealed record RiskPositionSummary(
    int RowCount,
    decimal TotalPnl,
    decimal TotalFxExposure,
    IReadOnlyList<RiskPositionGroup> Groups);
