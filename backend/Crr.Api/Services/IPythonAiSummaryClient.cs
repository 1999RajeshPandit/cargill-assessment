using Crr.Api.Models;

namespace Crr.Api.Services;

public interface IPythonAiSummaryClient
{
    Task<AiRiskSummaryResponse> GenerateAsync(RiskPositionSummary summary, CancellationToken cancellationToken);
}
