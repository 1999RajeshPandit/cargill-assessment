using System.Globalization;
using Crr.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Npgsql;
using NpgsqlTypes;

namespace Crr.Api.Services;

public sealed class RiskPositionCsvImporter(RiskPositionDbContext db)
{
    private static readonly string[] Headers =
    ["tradeId", "book", "commodity", "counterparty", "region", "instrumentType", "position", "currency", "marketPrice", "pnl", "fxExposure", "asOfDate"];

    public async Task<int> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("CSV input file was not found.", path);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var inserted = 0;
        var ids = (await db.RiskPositions.AsNoTracking().Select(x => x.TradeId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        using var parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields();
        if (headers is null || !headers.SequenceEqual(Headers, StringComparer.Ordinal))
            throw new InvalidDataException("CSV header does not match the expected risk_positions.csv schema.");

        await using (var importer = await connection.BeginBinaryImportAsync(
            "COPY risk_positions (trade_id, book, commodity, counterparty, region, instrument_type, position, currency, market_price, pnl, fx_exposure, as_of_date) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            while (!parser.EndOfData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fields = parser.ReadFields();
                if (fields is null || fields.Length != Headers.Length)
                    throw new InvalidDataException($"CSV row {inserted + 2} does not contain exactly {Headers.Length} fields.");
                if (fields.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException($"CSV row {inserted + 2} contains an empty field.");
                if (!ids.Add(fields[0])) throw new InvalidDataException($"Duplicate tradeId '{fields[0]}' already exists or appears more than once in the CSV (row {inserted + 2}).");
                if (fields[0].Length > 64 || fields[1].Length > 128 || fields[2].Length > 128 || fields[3].Length > 256
                    || fields[4].Length > 64 || fields[5].Length > 64 || fields[7].Length > 8)
                    throw new InvalidDataException($"CSV row {inserted + 2} exceeds a column's maximum length.");
                if (!decimal.TryParse(fields[6], NumberStyles.Number, CultureInfo.InvariantCulture, out var position)
                    || !decimal.TryParse(fields[8], NumberStyles.Number, CultureInfo.InvariantCulture, out var marketPrice)
                    || !decimal.TryParse(fields[9], NumberStyles.Number, CultureInfo.InvariantCulture, out var pnl)
                    || !decimal.TryParse(fields[10], NumberStyles.Number, CultureInfo.InvariantCulture, out var fxExposure)
                    || !DateOnly.TryParseExact(fields[11], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOfDate))
                    throw new InvalidDataException($"CSV row {inserted + 2} contains an invalid decimal or date.");

                await importer.StartRowAsync(cancellationToken);
                await importer.WriteAsync(fields[0], NpgsqlDbType.Varchar, cancellationToken);
                for (var i = 1; i <= 5; i++) await importer.WriteAsync(fields[i], NpgsqlDbType.Varchar, cancellationToken);
                await importer.WriteAsync(position, NpgsqlDbType.Numeric, cancellationToken);
                await importer.WriteAsync(fields[7], NpgsqlDbType.Varchar, cancellationToken);
                await importer.WriteAsync(marketPrice, NpgsqlDbType.Numeric, cancellationToken);
                await importer.WriteAsync(pnl, NpgsqlDbType.Numeric, cancellationToken);
                await importer.WriteAsync(fxExposure, NpgsqlDbType.Numeric, cancellationToken);
                await importer.WriteAsync(asOfDate, NpgsqlDbType.Date, cancellationToken);
                inserted++;
            }
            await importer.CompleteAsync(cancellationToken);
        }

        if (inserted == 0) throw new InvalidDataException("CSV contains no data rows.");
        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }
}
