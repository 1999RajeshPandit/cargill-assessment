using Microsoft.AspNetCore.Mvc;

namespace Crr.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Checks that the API is responding.</summary>
    /// <returns>A simple health status.</returns>
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
