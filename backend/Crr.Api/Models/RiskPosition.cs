namespace Crr.Api.Models;

public sealed record RiskPosition(
    string TradeId,
    string Book,
    string Commodity,
    string Counterparty,
    string Region,
    string InstrumentType,
    decimal Position,
    string Currency,
    decimal MarketPrice,
    decimal Pnl,
    decimal FxExposure,
    DateOnly AsOfDate);
