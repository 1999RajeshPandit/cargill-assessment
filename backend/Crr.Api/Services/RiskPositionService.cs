using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Crr.Api.Data;
using Crr.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Crr.Api.Services;

public sealed class RiskPositionService(RiskPositionDbContext db) : IRiskPositionService
{
    private static readonly HashSet<string> SortableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "tradeId", "book", "commodity", "counterparty", "region", "instrumentType",
        "position", "currency", "marketPrice", "pnl", "fxExposure", "asOfDate"
    };
    private static readonly char[] CsvSpecialCharacters = [',', '"', '\r', '\n'];

    public async Task<RiskPositionPage> GetPageAsync(int startRow, int endRow, string? sortModel, string? filterModel, string? search, CancellationToken cancellationToken)
    {
        var query = ApplyFilters(db.RiskPositions.AsNoTracking(), filterModel, search);
        var totalCount = await query.CountAsync(cancellationToken);
        var sort = ParseSort(sortModel);
        var ordered = ApplySort(query, sort);
        var start = Math.Clamp(startRow, 0, totalCount);
        var length = Math.Clamp(endRow - startRow, 0, 1_000);
        var rows = await ordered.Skip(start).Take(length).ToListAsync(cancellationToken);
        return new RiskPositionPage(rows, totalCount);
    }

    public async Task<RiskPositionSummary> GetSummaryAsync(string? groupBy, string? filterModel, string? search, CancellationToken cancellationToken)
    {
        var rows = ApplyFilters(db.RiskPositions.AsNoTracking(), filterModel, search);
        var totals = await rows.GroupBy(_ => 1).Select(g => new
        {
            RowCount = g.Count(),
            Pnl = g.Sum(x => x.Pnl),
            FxExposure = g.Sum(x => x.FxExposure)
        }).SingleOrDefaultAsync(cancellationToken);

        var groupByBook = groupBy?.Equals("book", StringComparison.OrdinalIgnoreCase) == true;
        var groups = groupByBook
            ? await rows.GroupBy(x => x.Book)
                .OrderByDescending(g => g.Sum(x => x.Pnl) < 0 ? -g.Sum(x => x.Pnl) : g.Sum(x => x.Pnl))
                .Select(g => new RiskPositionGroup(g.Key, g.Count(), g.Sum(x => x.Pnl), g.Sum(x => x.FxExposure), null))
                .ToArrayAsync(cancellationToken)
            : await rows.GroupBy(x => x.Commodity)
                .OrderByDescending(g => g.Sum(x => x.Pnl) < 0 ? -g.Sum(x => x.Pnl) : g.Sum(x => x.Pnl))
                .Select(g => new RiskPositionGroup(g.Key, g.Count(), g.Sum(x => x.Pnl), g.Sum(x => x.FxExposure), g.Sum(x => x.Position)))
                .ToArrayAsync(cancellationToken);

        return new RiskPositionSummary(totals?.RowCount ?? 0, totals?.Pnl ?? 0m, totals?.FxExposure ?? 0m, groups);
    }

    public async Task<byte[]> ExportCsvAsync(string? sortModel, string? filterModel, string? search, CancellationToken cancellationToken)
    {
        var rows = await ApplySort(ApplyFilters(db.RiskPositions.AsNoTracking(), filterModel, search), ParseSort(sortModel))
            .ToListAsync(cancellationToken);
        var csv = new StringBuilder(rows.Count * 128);
        csv.AppendLine("tradeId,book,commodity,counterparty,region,instrumentType,position,currency,marketPrice,pnl,fxExposure,asOfDate");
        foreach (var row in rows)
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

    private static IQueryable<RiskPosition> ApplyFilters(IQueryable<RiskPosition> query, string? filterJson, string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern(search.Trim(), "contains");
            query = query.Where(x => EF.Functions.ILike(x.Counterparty, pattern)
                || EF.Functions.ILike(x.Commodity, pattern) || EF.Functions.ILike(x.TradeId, pattern));
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
                    "tradeId" => ApplyText(query, x => x.TradeId, model),
                    "book" => ApplyText(query, x => x.Book, model),
                    "commodity" => ApplyText(query, x => x.Commodity, model),
                    "counterparty" => ApplyText(query, x => x.Counterparty, model),
                    "region" => ApplyText(query, x => x.Region, model),
                    "instrumentType" => ApplyText(query, x => x.InstrumentType, model),
                    "currency" => ApplyText(query, x => x.Currency, model),
                    "position" => ApplyNumber(query, x => x.Position, model),
                    "marketPrice" => ApplyNumber(query, x => x.MarketPrice, model),
                    "pnl" => ApplyNumber(query, x => x.Pnl, model),
                    "fxExposure" => ApplyNumber(query, x => x.FxExposure, model),
                    "asOfDate" => ApplyDate(query, x => x.AsOfDate, model),
                    _ => query
                };
            }
        }
        catch (JsonException) { /* Preserve the existing behavior for malformed filters. */ }
        return query;
    }

    private static IQueryable<RiskPosition> ApplyText(IQueryable<RiskPosition> query, Expression<Func<RiskPosition, string>> selector, JsonElement model)
    {
        var body = BuildText(selector.Body, model);
        return query.Where(Expression.Lambda<Func<RiskPosition, bool>>(body, selector.Parameters));
    }

    private static Expression BuildText(Expression value, JsonElement model)
    {
        if (model.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
            return Combine(values.EnumerateArray().Select(v => TextEquals(value, v.GetString() ?? "")).ToArray(), false);
        if (TryConditions(model, out var conditions, out var useOr))
            return Combine(conditions.Select(c => BuildText(value, c)).ToArray(), useOr);
        var type = model.TryGetProperty("type", out var t) ? t.GetString() : "contains";
        var term = model.TryGetProperty("filter", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() ?? "" : "";
        if (type == "equals") return TextEquals(value, term);
        if (type == "notEqual") return Expression.Not(TextEquals(value, term));
        return ILike(value, LikePattern(term, type switch { "startsWith" => "startsWith", "endsWith" => "endsWith", _ => "contains" }));
    }

    private static IQueryable<RiskPosition> ApplyNumber(IQueryable<RiskPosition> query, Expression<Func<RiskPosition, decimal>> selector, JsonElement model)
    {
        var body = BuildNumber(selector.Body, model);
        return query.Where(Expression.Lambda<Func<RiskPosition, bool>>(body, selector.Parameters));
    }

    private static Expression BuildNumber(Expression value, JsonElement model)
    {
        if (TryConditions(model, out var conditions, out var useOr)) return Combine(conditions.Select(c => BuildNumber(value, c)).ToArray(), useOr);
        if (!model.TryGetProperty("filter", out var filter) || !TryDecimal(filter, out var number)) return Expression.Constant(true);
        var type = model.TryGetProperty("type", out var t) ? t.GetString() : "equals";
        var left = Expression.Convert(value, typeof(decimal));
        var right = Expression.Constant(number);
        return type switch
        {
            "notEqual" => Expression.NotEqual(left, right),
            "lessThan" => Expression.LessThan(left, right),
            "lessThanOrEqual" => Expression.LessThanOrEqual(left, right),
            "greaterThan" => Expression.GreaterThan(left, right),
            "greaterThanOrEqual" => Expression.GreaterThanOrEqual(left, right),
            "inRange" when model.TryGetProperty("filterTo", out var to) && TryDecimal(to, out var upper) => Expression.AndAlso(Expression.GreaterThanOrEqual(left, right), Expression.LessThanOrEqual(left, Expression.Constant(upper))),
            _ => Expression.Equal(left, right)
        };
    }

    private static IQueryable<RiskPosition> ApplyDate(IQueryable<RiskPosition> query, Expression<Func<RiskPosition, DateOnly>> selector, JsonElement model)
    {
        var body = BuildDate(selector.Body, model);
        return query.Where(Expression.Lambda<Func<RiskPosition, bool>>(body, selector.Parameters));
    }

    private static Expression BuildDate(Expression value, JsonElement model)
    {
        if (TryConditions(model, out var conditions, out var useOr)) return Combine(conditions.Select(c => BuildDate(value, c)).ToArray(), useOr);
        if (!model.TryGetProperty("dateFrom", out var dateValue) || !DateOnly.TryParse(dateValue.GetString(), CultureInfo.InvariantCulture, out var date)) return Expression.Constant(true);
        var left = Expression.Convert(value, typeof(DateOnly));
        var right = Expression.Constant(date);
        var type = model.TryGetProperty("type", out var t) ? t.GetString() : "equals";
        return type switch
        {
            "notEqual" => Expression.NotEqual(left, right),
            "lessThan" => Expression.LessThan(left, right),
            "greaterThan" => Expression.GreaterThan(left, right),
            "inRange" when model.TryGetProperty("dateTo", out var to) && DateOnly.TryParse(to.GetString(), CultureInfo.InvariantCulture, out var upper) => Expression.AndAlso(Expression.GreaterThanOrEqual(left, right), Expression.LessThanOrEqual(left, Expression.Constant(upper))),
            _ => Expression.Equal(left, right)
        };
    }

    private static bool TryConditions(JsonElement model, out IReadOnlyList<JsonElement> conditions, out bool useOr)
    {
        useOr = model.TryGetProperty("operator", out var op) && op.GetString() == "OR";
        if (model.TryGetProperty("conditions", out var many) && many.ValueKind == JsonValueKind.Array) { conditions = many.EnumerateArray().Select(x => x.Clone()).ToArray(); return true; }
        if (model.TryGetProperty("condition1", out var first) && model.TryGetProperty("condition2", out var second))
        {
            conditions = [first.Clone(), second.Clone()];
            return true;
        }
        conditions = [];
        return false;
    }

    private static Expression Combine(IReadOnlyList<Expression> expressions, bool useOr)
    {
        if (expressions.Count == 0) return Expression.Constant(!useOr);
        return expressions.Aggregate((left, right) => useOr ? Expression.OrElse(left, right) : Expression.AndAlso(left, right));
    }

    private static Expression TextEquals(Expression value, string term) => ILike(value, LikePattern(term, "equals"));

    private static Expression ILike(Expression value, string pattern)
    {
        var method = typeof(NpgsqlDbFunctionsExtensions).GetMethod(nameof(NpgsqlDbFunctionsExtensions.ILike), [typeof(DbFunctions), typeof(string), typeof(string)])!;
        return Expression.Call(method, Expression.Constant(EF.Functions), value, Expression.Constant(pattern));
    }

    private static string LikePattern(string value, string type)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return type switch { "contains" => $"%{escaped}%", "startsWith" => $"{escaped}%", "endsWith" => $"%{escaped}", _ => escaped };
    }

    private static bool TryDecimal(JsonElement element, out decimal value) => element.ValueKind == JsonValueKind.Number
        ? element.TryGetDecimal(out value)
        : decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static IOrderedQueryable<RiskPosition> ApplySort(IQueryable<RiskPosition> query, (string Column, bool Descending)? sort)
    {
        var column = sort?.Column.ToLowerInvariant() ?? "tradeid";
        var descending = sort?.Descending ?? false;
        IOrderedQueryable<RiskPosition> ordered = (column, descending) switch
        {
            ("book", false) => query.OrderBy(x => x.Book), ("book", true) => query.OrderByDescending(x => x.Book),
            ("commodity", false) => query.OrderBy(x => x.Commodity), ("commodity", true) => query.OrderByDescending(x => x.Commodity),
            ("counterparty", false) => query.OrderBy(x => x.Counterparty), ("counterparty", true) => query.OrderByDescending(x => x.Counterparty),
            ("region", false) => query.OrderBy(x => x.Region), ("region", true) => query.OrderByDescending(x => x.Region),
            ("instrumenttype", false) => query.OrderBy(x => x.InstrumentType), ("instrumenttype", true) => query.OrderByDescending(x => x.InstrumentType),
            ("position", false) => query.OrderBy(x => x.Position), ("position", true) => query.OrderByDescending(x => x.Position),
            ("currency", false) => query.OrderBy(x => x.Currency), ("currency", true) => query.OrderByDescending(x => x.Currency),
            ("marketprice", false) => query.OrderBy(x => x.MarketPrice), ("marketprice", true) => query.OrderByDescending(x => x.MarketPrice),
            ("pnl", false) => query.OrderBy(x => x.Pnl), ("pnl", true) => query.OrderByDescending(x => x.Pnl),
            ("fxexposure", false) => query.OrderBy(x => x.FxExposure), ("fxexposure", true) => query.OrderByDescending(x => x.FxExposure),
            ("asofdate", false) => query.OrderBy(x => x.AsOfDate), ("asofdate", true) => query.OrderByDescending(x => x.AsOfDate),
            (_, false) => query.OrderBy(x => x.TradeId), (_, true) => query.OrderByDescending(x => x.TradeId)
        };
        return column == "tradeid" ? ordered : ordered.ThenBy(x => x.TradeId);
    }

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
        catch (JsonException) { return null; }
    }

    private static string Csv(string value) => value.IndexOfAny(CsvSpecialCharacters) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
