using Crr.Api.Models;

namespace Crr.Api.Services;

public interface IRiskPositionService
{
    Task<RiskPositionPage> GetPageAsync(int startRow, int endRow, string? sortModel, string? filterModel, string? search, CancellationToken cancellationToken);
    Task<RiskPositionSummary> GetSummaryAsync(string? groupBy, string? filterModel, string? search, CancellationToken cancellationToken);
    Task<byte[]> ExportCsvAsync(string? sortModel, string? filterModel, string? search, CancellationToken cancellationToken);
}
