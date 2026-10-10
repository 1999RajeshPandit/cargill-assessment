using System.Net.Http.Json;
using Crr.Api.Models;

namespace Crr.Api.Services;

public sealed class PythonAiSummaryClient(HttpClient httpClient) : IPythonAiSummaryClient
{
    public async Task<AiRiskSummaryResponse> GenerateAsync(
        RiskPositionSummary summary,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/ai/risk-summary",
            summary,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AiRiskSummaryResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The Python AI service returned an empty response.");
    }
}
