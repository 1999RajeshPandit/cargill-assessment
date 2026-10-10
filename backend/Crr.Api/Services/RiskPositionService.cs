using System.Globalization;
using System.Text;
using System.Text.Json;
using Crr.Api.Models;
using Crr.Api.Repositories;

namespace Crr.Api.Services;

public sealed class RiskPositionService(IRiskPositionRepository repository) : IRiskPositionService
{
    private static readonly HashSet<string> SortableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "tradeId", "book", "commodity", "counterparty", "region", "instrumentType",
        "position", "currency", "marketPrice", "pnl", "fxExposure", "asOfDate"
    };
    private static readonly char[] CsvSpecialCharacters = [',', '"', '\r', '\n'];

    public RiskPositionPage GetPage(int startRow, int endRow, string? sortModel, string? filterModel, string? search)
    {
        var filtered = ApplyFilters(repository.GetAll(), filterModel, search).ToArray();
        var sort = ParseSort(sortModel);
        IEnumerable<RiskPosition> ordered = sort is null
            ? filtered.OrderBy(x => x.TradeId, StringComparer.Ordinal)
            : Sort(filtered, sort.Value.Column, sort.Value.Descending);

        var start = Math.Clamp(startRow, 0, filtered.Length);
        var length = Math.Clamp(endRow - startRow, 0, 1_000);
        return new RiskPositionPage(ordered.Skip(start).Take(length).ToArray(), filtered.Length);
    }

    public RiskPositionSummary GetSummary(string? groupBy, string? filterModel, string? search)
    {
        var rows = ApplyFilters(repository.GetAll(), filterModel, search).ToArray();
        var groupByBook = groupBy?.Equals("book", StringComparison.OrdinalIgnoreCase) == true;
        var groups = rows.GroupBy(x => groupByBook ? x.Book : x.Commodity)
            .Select(group => new RiskPositionGroup(
                group.Key,
                group.Count(),
                group.Sum(x => x.Pnl),
                group.Sum(x => x.FxExposure),
                // Avoid adding quantities across different commodities in a trading book.
                groupByBook ? null : group.Sum(x => x.Position)))
            .OrderByDescending(x => Math.Abs(x.Pnl))
            .ToArray();

        return new RiskPositionSummary(
            rows.Length,
            rows.Sum(x => x.Pnl),
            rows.Sum(x => x.FxExposure),
            groups);
    }

    public byte[] ExportCsv(string? sortModel, string? filterModel, string? search)
    {
        var rows = ApplyFilters(repository.GetAll(), filterModel, search).ToArray();
        var sort = ParseSort(sortModel);
        IEnumerable<RiskPosition> ordered = sort is null
            ? rows.OrderBy(x => x.TradeId, StringComparer.Ordinal)
            : Sort(rows, sort.Value.Column, sort.Value.Descending);

        var csv = new StringBuilder(rows.Length * 128);
        csv.AppendLine("tradeId,book,commodity,counterparty,region,instrumentType,position,currency,marketPrice,pnl,fxExposure,asOfDate");
        foreach (var row in ordered)
        {
            csv.Append(Csv(row.TradeId)).Append(',').Append(Csv(row.Book)).Append(',').Append(Csv(row.Commodity)).Append(',')
                .Append(Csv(row.Counterparty)).Append(',').Append(Csv(row.Region)).Append(',').Append(Csv(row.InstrumentType)).Append(',')
                .Append(row.Position.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Csv(row.Currency)).Append(',')
                .Append(row.MarketPrice.ToString(CultureInfo.InvariantCulture)).Append(',').Append(row.Pnl.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.FxExposure.ToString(CultureInfo.InvariantCulture)).Append(',')
                .AppendLine(row.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static IEnumerable<RiskPosition> ApplyFilters(IEnumerable<RiskPosition> source, string? filterJson, string? search)
    {
        var query = source;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => Contains(x.Counterparty, term) || Contains(x.Commodity, term) || Contains(x.TradeId, term));
        }

        if (string.IsNullOrWhiteSpace(filterJson)) return query;
        try
        {
            using var document = JsonDocument.Parse(filterJson);
            foreach (var filter in document.RootElement.EnumerateObject())
            {
                var field = filter.Name;
                var model = filter.Value.Clone();
                query = field switch
                {
                    "tradeId" => query.Where(x => TextMatch(x.TradeId, model)),
                    "book" => query.Where(x => TextMatch(x.Book, model)),
                    "commodity" => query.Where(x => TextMatch(x.Commodity, model)),
                    "counterparty" => query.Where(x => TextMatch(x.Counterparty, model)),
                    "region" => query.Where(x => TextMatch(x.Region, model)),
                    "instrumentType" => query.Where(x => TextMatch(x.InstrumentType, model)),
                    "currency" => query.Where(x => TextMatch(x.Currency, model)),
                    "position" => query.Where(x => NumberMatch(x.Position, model)),
                    "marketPrice" => query.Where(x => NumberMatch(x.MarketPrice, model)),
                    "pnl" => query.Where(x => NumberMatch(x.Pnl, model)),
                    "fxExposure" => query.Where(x => NumberMatch(x.FxExposure, model)),
                    "asOfDate" => query.Where(x => DateMatch(x.AsOfDate, model)),
                    _ => query
                };
            }
        }
        catch (JsonException)
        {
            // Ignore malformed client filter payloads; the grid still receives data.
        }

        return query;
    }

    private static bool TextMatch(string value, JsonElement model)
    {
        if (model.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
            return values.EnumerateArray().Any(v => string.Equals(value, v.GetString(), StringComparison.OrdinalIgnoreCase));
        if (model.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
        {
            var matches = conditions.EnumerateArray().Select(condition => TextMatch(value, condition));
            return model.TryGetProperty("operator", out var op) && op.GetString() == "OR" ? matches.Any(x => x) : matches.All(x => x);
        }
        if (model.TryGetProperty("condition1", out var condition1))
        {
            var first = TextMatch(value, condition1);
            var second = model.TryGetProperty("condition2", out var condition2) && TextMatch(value, condition2);
            return model.TryGetProperty("operator", out var op) && op.GetString() == "OR" ? first || second : first && second;
        }

        var type = model.TryGetProperty("type", out var filterType) ? filterType.GetString() : "contains";
        var term = model.TryGetProperty("filter", out var filter) && filter.ValueKind == JsonValueKind.String
            ? filter.GetString() ?? ""
            : "";
        return type switch
        {
            "equals" => value.Equals(term, StringComparison.OrdinalIgnoreCase),
            "notEqual" => !value.Equals(term, StringComparison.OrdinalIgnoreCase),
            "startsWith" => value.StartsWith(term, StringComparison.OrdinalIgnoreCase),
            "endsWith" => value.EndsWith(term, StringComparison.OrdinalIgnoreCase),
            _ => Contains(value, term)
        };
    }

    private static bool NumberMatch(decimal value, JsonElement model)
    {
        if (model.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array)
        {
            var matches = conditions.EnumerateArray().Select(condition => NumberMatch(value, condition));
            return model.TryGetProperty("operator", out var op) && op.GetString() == "OR" ? matches.Any(x => x) : matches.All(x => x);
        }
        if (model.TryGetProperty("condition1", out var condition1))
        {
            var first = NumberMatch(value, condition1);
            var second = model.TryGetProperty("condition2", out var condition2) && NumberMatch(value, condition2);
            return model.TryGetProperty("operator", out var op) && op.GetString() == "OR" ? first || second : first && second;
        }
        if (!model.TryGetProperty("filter", out var filter) || !TryDecimal(filter, out var number)) return true;

        var type = model.TryGetProperty("type", out var filterType) ? filterType.GetString() : "equals";
        return type switch
        {
            "notEqual" => value != number,
            "lessThan" => value < number,
            "lessThanOrEqual" => value <= number,
            "greaterThan" => value > number,
            "greaterThanOrEqual" => value >= number,
            "inRange" => model.TryGetProperty("filterTo", out var to) && TryDecimal(to, out var upper) && value >= number && value <= upper,
            _ => value == number
        };
    }

    private static bool DateMatch(DateOnly value, JsonElement model)
    {
        if (!model.TryGetProperty("dateFrom", out var dateElement) || !DateOnly.TryParse(dateElement.GetString(), out var date)) return true;
        var type = model.TryGetProperty("type", out var filterType) ? filterType.GetString() : "equals";
        return type switch
        {
            "notEqual" => value != date,
            "lessThan" => value < date,
            "greaterThan" => value > date,
            "inRange" when model.TryGetProperty("dateTo", out var to) && DateOnly.TryParse(to.GetString(), out var upper) => value >= date && value <= upper,
            _ => value == date
        };
    }

    private static bool TryDecimal(JsonElement element, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Number) return element.TryGetDecimal(out value);
        return decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool Contains(string value, string term) => value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string Csv(string value) => value.IndexOfAny(CsvSpecialCharacters) >= 0
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;

    private static (string Column, bool Descending)? ParseSort(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var first = document.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Object || !first.TryGetProperty("colId", out var column)) return null;
            var name = column.GetString();
            if (name is null || !SortableColumns.Contains(name)) return null;
            return (name, first.TryGetProperty("sort", out var direction) && direction.GetString() == "desc");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IOrderedEnumerable<RiskPosition> Sort(IEnumerable<RiskPosition> rows, string column, bool descending)
    {
        Func<RiskPosition, object> key = column.ToLowerInvariant() switch
        {
            "tradeid" => x => x.TradeId,
            "book" => x => x.Book,
            "commodity" => x => x.Commodity,
            "counterparty" => x => x.Counterparty,
            "region" => x => x.Region,
            "instrumenttype" => x => x.InstrumentType,
            "position" => x => x.Position,
            "currency" => x => x.Currency,
            "marketprice" => x => x.MarketPrice,
            "pnl" => x => x.Pnl,
            "fxexposure" => x => x.FxExposure,
            "asofdate" => x => x.AsOfDate,
            _ => x => x.TradeId
        };
        return descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }
}
