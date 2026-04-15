using CollaborationEngine.API.Models;
using CollaborationEngine.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollaborationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILocalDataService _dataService;

    public AuthController(ILocalDataService dataService)
    {
        _dataService = dataService;
    }

    [HttpPost("login/user")]
    public async Task<IActionResult> LoginUser([FromBody] UserLoginRequest request)
    {
        var (token, user) = await DemoAuthService.LoginUserAsync(request.Email, request.ApiKey);
        return Ok(new { Token = token, UserType = "User", User = user });
    }

    [HttpPost("login/agent")]
    public async Task<IActionResult> LoginAgent([FromBody] AgentLoginRequest request)
    {
        var applicationId = await ResolveApplicationIdAsync(request.ApiKey, request.ApplicationName);
        var agents = await _dataService.GetAgentsAsync();
        var existing = agents.FirstOrDefault(a => string.Equals(a.Email, request.Email, StringComparison.OrdinalIgnoreCase) && a.ApplicationId == applicationId);

        if (existing == null)
        {
            var known = agents
                .Where(a => a.ApplicationId == applicationId)
                .Select(a => a.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e)
                .ToList();

            return BadRequest($"Unknown agent email. Seed an agent in Data/agents.json first. Known: [{string.Join(", ", known)}]");
        }

        var agent = existing;

        agent.IsActive = true;
        agent.IsAvailable = true;
        agent.LastActiveAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;

        var saved = await _dataService.SaveAgentAsync(agent);
        var token = $"demo-agent-token-{DateTime.UtcNow.Ticks}";
        return Ok(new { Token = token, UserType = "Agent", Agent = saved });
    }

    [HttpPost("login/supervisor")]
    public async Task<IActionResult> LoginSupervisor([FromBody] SupervisorLoginRequest request)
    {
        var applicationId = await ResolveApplicationIdAsync(request.ApiKey, request.ApplicationName);
        var supervisors = await _dataService.GetSupervisorsAsync();
        var existing = supervisors.FirstOrDefault(s => string.Equals(s.Email, request.Email, StringComparison.OrdinalIgnoreCase) && s.ApplicationId == applicationId);

        if (existing == null)
        {
            var known = supervisors
                .Where(s => s.ApplicationId == applicationId)
                .Select(s => s.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e)
                .ToList();

            return BadRequest($"Unknown supervisor email. Seed a supervisor in Data/supervisors.json first. Known: [{string.Join(", ", known)}]");
        }

        var supervisor = existing;

        supervisor.IsActive = true;
        supervisor.IsAvailable = true;
        supervisor.LastActiveAt = DateTime.UtcNow;
        supervisor.UpdatedAt = DateTime.UtcNow;

        var saved = await _dataService.SaveSupervisorAsync(supervisor);
        var token = $"demo-supervisor-token-{DateTime.UtcNow.Ticks}";
        return Ok(new { Token = token, UserType = "Supervisor", Supervisor = saved });
    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken([FromBody] string token)
    {
        var (userId, userType) = await DemoAuthService.GetTokenInfoAsync(token);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("Invalid token");
        }
        return Ok(new { UserId = userId, UserType = userType });
    }

    private async Task<int> ResolveApplicationIdAsync(string? apiKey, string? applicationName)
    {
        var apps = await _dataService.GetApplicationsAsync();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var byKey = apps.FirstOrDefault(a => string.Equals(a.ApiKey, apiKey.Trim(), StringComparison.Ordinal));
            if (byKey != null)
            {
                return byKey.Id;
            }
        }

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            var normalized = applicationName.Trim();
            var byName = apps.FirstOrDefault(a => string.Equals(a.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
            {
                return byName.Id;
            }
        }

        return 1;
    }
}

public record UserLoginRequest(string Email, string ApiKey, string? ApplicationName = null);
public record AgentLoginRequest(string Email, string ApiKey, string? ApplicationName = null);
public record SupervisorLoginRequest(string Email, string ApiKey, string? ApplicationName = null);
