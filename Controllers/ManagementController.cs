using CollaborationEngine.API.Models;
using CollaborationEngine.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollaborationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ManagementController : ControllerBase
{
    private readonly ILocalDataService _dataService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly ISupervisorAssignmentService _supervisorAssignmentService;

    public ManagementController(
        ILocalDataService dataService,
        IAgentAssignmentService agentAssignmentService,
        ISupervisorAssignmentService supervisorAssignmentService)
    {
        _dataService = dataService;
        _agentAssignmentService = agentAssignmentService;
        _supervisorAssignmentService = supervisorAssignmentService;
    }

    [HttpPost("application/register")]
    public async Task<IActionResult> RegisterApplication([FromBody] RegisterApplicationRequest request)
    {
        var existingApps = await _dataService.GetApplicationsAsync();
        var existingApp = existingApps.FirstOrDefault(a => a.Name == request.Name);

        if (existingApp != null)
        {
            return BadRequest("Application with this name already exists");
        }

        var application = new Application
        {
            Name = request.Name,
            Description = request.Description,
            ApiKey = GenerateApiKey(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedApp = await _dataService.SaveApplicationAsync(application);
        return Ok(new { 
            savedApp.Id,
            savedApp.Name,
            savedApp.Description,
            savedApp.ApiKey
        });
    }

    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications()
    {
        var apps = await _dataService.GetApplicationsAsync();
        var active = apps
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Description
            })
            .ToList();

        return Ok(active);
    }

    [HttpPost("user/register")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterUserRequest request)
    {
        var application = await ResolveApplicationAsync(request.ApiKey, request.ApplicationName);

        var users = await _dataService.GetUsersAsync();
        var existingUser = users.FirstOrDefault(u => u.Email == request.Email && u.ApplicationId == application.Id);

        if (existingUser != null)
        {
            return BadRequest("User with this email already exists for this application");
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AdditionalDetails = request.AdditionalDetails,
            ApplicationId = application.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedUser = await _dataService.SaveUserAsync(user);
        return Ok(new { 
            savedUser.Id,
            savedUser.Name,
            savedUser.Email,
            savedUser.PhoneNumber
        });
    }

    [HttpPost("user/register/simple")]
    public async Task<IActionResult> RegisterUserSimple([FromBody] RegisterUserSimpleRequest request)
    {
        var application = await ResolveApplicationAsync(null, request.ApplicationName);

        var users = await _dataService.GetUsersAsync();
        var existingUser = users.FirstOrDefault(u => u.Email == request.Email && u.ApplicationId == application.Id);
        if (existingUser != null)
        {
            return Ok(new
            {
                existingUser.Id,
                existingUser.Name,
                existingUser.Email,
                existingUser.PhoneNumber
            });
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            ApplicationId = application.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedUser = await _dataService.SaveUserAsync(user);
        return Ok(new
        {
            savedUser.Id,
            savedUser.Name,
            savedUser.Email,
            savedUser.PhoneNumber
        });
    }

    [HttpPost("agent/register")]
    public async Task<IActionResult> RegisterAgent([FromBody] RegisterAgentRequest request)
    {
        var application = await ResolveApplicationAsync(request.ApiKey, request.ApplicationName);

        var agents = await _dataService.GetAgentsAsync();
        var existingAgent = agents.FirstOrDefault(a => a.Email == request.Email && a.ApplicationId == application.Id);

        if (existingAgent != null)
        {
            return BadRequest("Agent with this email already exists for this application");
        }

        var agent = new Agent
        {
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Department = request.Department,
            ApplicationId = application.Id,
            IsAvailable = true,
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedAgent = await _dataService.SaveAgentAsync(agent);
        return Ok(new { 
            savedAgent.Id,
            savedAgent.Name,
            savedAgent.Email,
            savedAgent.PhoneNumber,
            savedAgent.Department
        });
    }

    [HttpPost("supervisor/register")]
    public async Task<IActionResult> RegisterSupervisor([FromBody] RegisterSupervisorRequest request)
    {
        var application = await ResolveApplicationAsync(request.ApiKey, request.ApplicationName);

        var supervisors = await _dataService.GetSupervisorsAsync();
        var existingSupervisor = supervisors.FirstOrDefault(s => s.Email == request.Email && s.ApplicationId == application.Id);

        if (existingSupervisor != null)
        {
            return BadRequest("Supervisor with this email already exists for this application");
        }

        var supervisor = new Supervisor
        {
            Name = request.Name,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Department = request.Department,
            ApplicationId = application.Id,
            IsAvailable = true,
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedSupervisor = await _dataService.SaveSupervisorAsync(supervisor);
        return Ok(new { 
            savedSupervisor.Id,
            savedSupervisor.Name,
            savedSupervisor.Email,
            savedSupervisor.PhoneNumber,
            savedSupervisor.Department
        });
    }

    [HttpGet("agents/available")]
    public async Task<IActionResult> GetAvailableAgents([FromQuery] string? appName = null, [FromQuery] string? apiKey = null)
    {
        var app = await ResolveApplicationAsync(apiKey, appName);
        var agents = await _agentAssignmentService.GetAvailableAgentsAsync(app.Id);
        return Ok(agents.Select(a => new
        {
            a.Id,
            a.Name,
            a.Email,
            a.Department,
            a.IsAvailable
        }));
    }

    [HttpGet("supervisors/available")]
    public async Task<IActionResult> GetAvailableSupervisors([FromQuery] int? currentAgentId = null, [FromQuery] string? appName = null, [FromQuery] string? apiKey = null)
    {
        var app = await ResolveApplicationAsync(apiKey, appName);
        var supervisors = await _supervisorAssignmentService.GetAvailableSupervisorsAsync(app.Id, currentAgentId);
        return Ok(supervisors.Select(s => new
        {
            s.Id,
            s.Name,
            s.Email,
            s.Department,
            s.IsAvailable
        }));
    }

    [HttpPost("agent/{agentId}/availability")]
    public async Task<IActionResult> UpdateAgentAvailability(int agentId, [FromBody] UpdateAvailabilityRequest request)
    {
        var agent = await _dataService.GetAgentAsync(agentId);
        if (agent == null)
        {
            return NotFound("Agent not found");
        }

        agent.IsAvailable = request.IsAvailable;
        agent.LastActiveAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;

        await _dataService.SaveAgentAsync(agent);
        return Ok(new { 
            agent.Id,
            agent.IsAvailable,
            agent.LastActiveAt
        });
    }

    [HttpPost("supervisor/{supervisorId}/availability")]
    public async Task<IActionResult> UpdateSupervisorAvailability(int supervisorId, [FromBody] UpdateAvailabilityRequest request)
    {
        var supervisor = await _dataService.GetSupervisorAsync(supervisorId);
        if (supervisor == null)
        {
            return NotFound("Supervisor not found");
        }

        supervisor.IsAvailable = request.IsAvailable;
        supervisor.LastActiveAt = DateTime.UtcNow;
        supervisor.UpdatedAt = DateTime.UtcNow;

        await _dataService.SaveSupervisorAsync(supervisor);
        return Ok(new { 
            supervisor.Id,
            supervisor.IsAvailable,
            supervisor.LastActiveAt
        });
    }

    private string GenerateApiKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var result = new char[32];
        
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(result);
    }

    private async Task<Application> GetOrCreateDefaultApplicationAsync()
    {
        var applications = await _dataService.GetApplicationsAsync();
        var application = applications.FirstOrDefault(a => a.IsActive);
        if (application != null)
        {
            return application;
        }

        var defaultApp = new Application
        {
            Name = "DemoApp",
            Description = "Local demo application",
            ApiKey = GenerateApiKey(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _dataService.SaveApplicationAsync(defaultApp);
    }

    private async Task<Application> ResolveApplicationAsync(string? apiKey, string? applicationName)
    {
        var applications = await _dataService.GetApplicationsAsync();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var byKey = applications.FirstOrDefault(a => string.Equals(a.ApiKey, apiKey.Trim(), StringComparison.Ordinal));
            if (byKey != null)
            {
                return byKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            var normalized = applicationName.Trim();
            var byName = applications.FirstOrDefault(a => string.Equals(a.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
            {
                return byName;
            }

            var created = new Application
            {
                Name = normalized,
                Description = "Auto created application",
                ApiKey = GenerateApiKey(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return await _dataService.SaveApplicationAsync(created);
        }

        return await GetOrCreateDefaultApplicationAsync();
    }
}

public record RegisterApplicationRequest(string Name, string Description);
public record RegisterUserRequest(string Name, string Email, string PhoneNumber, string? AdditionalDetails, string? ApiKey, string? ApplicationName);
public record RegisterUserSimpleRequest(string Name, string Email, string PhoneNumber, string? ApplicationName);
public record RegisterAgentRequest(string Name, string Email, string PhoneNumber, string? Department, string? ApiKey, string? ApplicationName);
public record RegisterSupervisorRequest(string Name, string Email, string PhoneNumber, string? Department, string? ApiKey, string? ApplicationName);
public record UpdateAvailabilityRequest(bool IsAvailable);
