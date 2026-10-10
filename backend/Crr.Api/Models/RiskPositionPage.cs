namespace Crr.Api.Models;

public sealed record RiskPositionPage(IReadOnlyList<RiskPosition> Rows, int TotalCount);
