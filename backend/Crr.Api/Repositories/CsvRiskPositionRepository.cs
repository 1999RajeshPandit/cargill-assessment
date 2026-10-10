using System.Globalization;
using Crr.Api.Models;
using Microsoft.VisualBasic.FileIO;

namespace Crr.Api.Repositories;

public sealed class CsvRiskPositionRepository : IRiskPositionRepository
{
    private readonly IReadOnlyList<RiskPosition> _positions;

    public CsvRiskPositionRepository()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "risk_positions.csv");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Risk positions CSV not found at {path}. Build the API project to copy the source data.");

        using var parser = new TextFieldParser(path)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(",");
        _ = parser.ReadFields(); // header

        var rows = new List<RiskPosition>(50_000);
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length < 12) continue;

            rows.Add(new RiskPosition(
                fields[0], fields[1], fields[2], fields[3], fields[4], fields[5],
                ParseDecimal(fields[6]), fields[7], ParseDecimal(fields[8]), ParseDecimal(fields[9]),
                ParseDecimal(fields[10]), DateOnly.Parse(fields[11], CultureInfo.InvariantCulture)));
        }

        _positions = rows.ToArray();
    }

    public IReadOnlyList<RiskPosition> GetAll() => _positions;

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
