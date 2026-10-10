using Crr.Api.Services;
using Crr.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Crr.Api.Controllers;

[ApiController]
[Route("api/risk-positions")]
public sealed class RiskPositionsController(
    IRiskPositionService riskPositionService,
    IPythonAiSummaryClient pythonAiSummaryClient) : ControllerBase
{
    /// <summary>Gets a page of risk positions with server-side filtering and sorting.</summary>
    /// <param name="startRow">Zero-based first row to return.</param>
    /// <param name="endRow">Exclusive row boundary; each response is capped at 1,000 rows.</param>
    /// <param name="sortModel">AG Grid sort model JSON array.</param>
    /// <param name="filterModel">AG Grid filter model JSON object.</param>
    /// <param name="search">Optional case-insensitive search over trade ID, counterparty, and commodity.</param>
    /// <returns>The requested page and the total number of matching rows.</returns>
    [ProducesResponseType(typeof(RiskPositionPage), StatusCodes.Status200OK)]
    [HttpGet]
    public IActionResult GetPage(
        [FromQuery] int? startRow,
        [FromQuery] int? endRow,
        [FromQuery] string? sortModel,
        [FromQuery] string? filterModel,
        [FromQuery(Name = "q")] string? search)
    {
        var start = Math.Max(startRow ?? 0, 0);
        var end = Math.Max(endRow ?? start + 100, start);
        return Ok(riskPositionService.GetPage(start, end, sortModel, filterModel, search));
    }

    /// <summary>Gets totals and grouping for all positions matching the active filters.</summary>
    /// <param name="groupBy">Grouping field: book or commodity. Defaults to commodity.</param>
    /// <param name="filterModel">AG Grid filter model JSON object.</param>
    /// <param name="search">Optional case-insensitive search over trade ID, counterparty, and commodity.</param>
    /// <returns>Filtered P&amp;L and FX totals with per-group aggregates.</returns>
    [ProducesResponseType(typeof(RiskPositionSummary), StatusCodes.Status200OK)]
    [HttpGet("summary")]
    public IActionResult GetSummary(
        [FromQuery] string? groupBy,
        [FromQuery] string? filterModel,
        [FromQuery(Name = "q")] string? search) =>
        Ok(riskPositionService.GetSummary(groupBy ?? "commodity", filterModel, search));

    /// <summary>Generates an AI narrative from the complete filtered risk summary.</summary>
    /// <param name="groupBy">Grouping field: book or commodity. Defaults to commodity.</param>
    /// <param name="filterModel">AG Grid filter model JSON object.</param>
    /// <param name="search">Optional case-insensitive search over trade ID, counterparty, and commodity.</param>
    /// <param name="cancellationToken">Cancels the request when the client disconnects.</param>
    /// <returns>An AI-generated narrative and the number of summarized positions.</returns>
    [ProducesResponseType(typeof(AiRiskSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [HttpGet("summary/ai")]
    public async Task<IActionResult> GetAiSummary(
        [FromQuery] string? groupBy,
        [FromQuery] string? filterModel,
        [FromQuery(Name = "q")] string? search,
        CancellationToken cancellationToken)
    {
        var filteredSummary = riskPositionService.GetSummary(groupBy ?? "commodity", filterModel, search);
        try
        {
            return Ok(await pythonAiSummaryClient.GenerateAsync(filteredSummary, cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            var statusCode = exception.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status502BadGateway;
            return StatusCode(statusCode, new
            {
                message = statusCode == StatusCodes.Status503ServiceUnavailable
                    ? "The Python AI service is not configured or unavailable."
                    : "The Python AI service could not generate a summary."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "The AI summary request timed out." });
        }
    }

    /// <summary>Downloads every matching risk position as a CSV file.</summary>
    /// <param name="sortModel">AG Grid sort model JSON array.</param>
    /// <param name="filterModel">AG Grid filter model JSON object.</param>
    /// <param name="search">Optional case-insensitive search over trade ID, counterparty, and commodity.</param>
    /// <returns>A CSV attachment containing the filtered positions.</returns>
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("export")]
    public IActionResult Export(
        [FromQuery] string? sortModel,
        [FromQuery] string? filterModel,
        [FromQuery(Name = "q")] string? search) =>
        File(riskPositionService.ExportCsv(sortModel, filterModel, search), "text/csv; charset=utf-8", "risk-positions.csv");
}
