namespace Crr.Api.Models;

public sealed class RiskPosition
{
    public RiskPosition() { }

    public RiskPosition(
        string tradeId, string book, string commodity, string counterparty, string region,
        string instrumentType, decimal position, string currency, decimal marketPrice,
        decimal pnl, decimal fxExposure, DateOnly asOfDate)
    {
        TradeId = tradeId;
        Book = book;
        Commodity = commodity;
        Counterparty = counterparty;
        Region = region;
        InstrumentType = instrumentType;
        Position = position;
        Currency = currency;
        MarketPrice = marketPrice;
        Pnl = pnl;
        FxExposure = fxExposure;
        AsOfDate = asOfDate;
    }

    public string TradeId { get; set; } = "";
    public string Book { get; set; } = "";
    public string Commodity { get; set; } = "";
    public string Counterparty { get; set; } = "";
    public string Region { get; set; } = "";
    public string InstrumentType { get; set; } = "";
    public decimal Position { get; set; }
    public string Currency { get; set; } = "";
    public decimal MarketPrice { get; set; }
    public decimal Pnl { get; set; }
    public decimal FxExposure { get; set; }
    public DateOnly AsOfDate { get; set; }
}
