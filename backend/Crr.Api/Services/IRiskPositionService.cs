using Crr.Api.Models;

namespace Crr.Api.Services;

public interface IRiskPositionService
{
    RiskPositionPage GetPage(int startRow, int endRow, string? sortModel, string? filterModel, string? search);
    RiskPositionSummary GetSummary(string? groupBy, string? filterModel, string? search);
    byte[] ExportCsv(string? sortModel, string? filterModel, string? search);
}
