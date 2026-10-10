using Crr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Crr.Api.Data;

public sealed class RiskPositionDbContext(DbContextOptions<RiskPositionDbContext> options) : DbContext(options)
{
    public DbSet<RiskPosition> RiskPositions => Set<RiskPosition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var position = modelBuilder.Entity<RiskPosition>();
        position.ToTable("risk_positions");
        position.HasKey(x => x.TradeId);
        position.Property(x => x.TradeId).HasColumnName("trade_id").HasMaxLength(64);
        position.Property(x => x.Book).HasColumnName("book").HasMaxLength(128).IsRequired();
        position.Property(x => x.Commodity).HasColumnName("commodity").HasMaxLength(128).IsRequired();
        position.Property(x => x.Counterparty).HasColumnName("counterparty").HasMaxLength(256).IsRequired();
        position.Property(x => x.Region).HasColumnName("region").HasMaxLength(64).IsRequired();
        position.Property(x => x.InstrumentType).HasColumnName("instrument_type").HasMaxLength(64).IsRequired();
        position.Property(x => x.Position).HasColumnName("position").HasPrecision(20, 4);
        position.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(8).IsRequired();
        position.Property(x => x.MarketPrice).HasColumnName("market_price").HasPrecision(20, 4);
        position.Property(x => x.Pnl).HasColumnName("pnl").HasPrecision(20, 4);
        position.Property(x => x.FxExposure).HasColumnName("fx_exposure").HasPrecision(20, 4);
        position.Property(x => x.AsOfDate).HasColumnName("as_of_date").HasColumnType("date");
        position.HasIndex(x => x.Book);
        position.HasIndex(x => x.Commodity);
        position.HasIndex(x => x.Counterparty);
        position.HasIndex(x => x.Region);
        position.HasIndex(x => x.AsOfDate);
    }
}
