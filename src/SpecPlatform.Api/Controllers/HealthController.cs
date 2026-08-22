using Microsoft.AspNetCore.Mvc;
using SpecPlatform.Shared.DTOs;

namespace SpecPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public HealthController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet]
    public ActionResult<HealthCheckResponse> GetHealth()
    {
        return Ok(new HealthCheckResponse
        {
            Status = "Healthy",
            Message = "API is alive",
            Timestamp = DateTime.UtcNow,
            Environment = _env.EnvironmentName
        });
    }
}
