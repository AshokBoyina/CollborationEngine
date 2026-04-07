using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public class DemoAuthService
{
    public static async Task<(string token, object user)> LoginUserAsync(string email, string apiKey)
    {
        // Demo authentication - accept any email and API key
        var user = new User
        {
            Id = 1,
            Name = "Demo User",
            Email = email,
            PhoneNumber = "123-456-7890",
            ApplicationId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var token = $"demo-user-token-{DateTime.UtcNow.Ticks}";
        return await Task.FromResult((token, (object)user));
    }

    public static async Task<(string token, object user)> LoginAgentAsync(string email, string apiKey)
    {
        // Demo authentication - accept any email and API key
        var agent = new Agent
        {
            Id = 1,
            Name = "Demo Agent",
            Email = email,
            PhoneNumber = "123-456-7890",
            Department = "Support",
            ApplicationId = 1,
            IsAvailable = true,
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var token = $"demo-agent-token-{DateTime.UtcNow.Ticks}";
        return await Task.FromResult((token, (object)agent));
    }

    public static async Task<(string token, object user)> LoginSupervisorAsync(string email, string apiKey)
    {
        // Demo authentication - accept any email and API key
        var supervisor = new Supervisor
        {
            Id = 1,
            Name = "Demo Supervisor",
            Email = email,
            PhoneNumber = "123-456-7890",
            Department = "Management",
            ApplicationId = 1,
            IsAvailable = true,
            IsActive = true,
            LastActiveAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var token = $"demo-supervisor-token-{DateTime.UtcNow.Ticks}";
        return await Task.FromResult((token, (object)supervisor));
    }

    public static async Task<(string userId, string userType)> GetTokenInfoAsync(string token)
    {
        if (token.Contains("user"))
            return await Task.FromResult(("1", "User"));
        else if (token.Contains("agent"))
            return await Task.FromResult(("1", "Agent"));
        else if (token.Contains("supervisor"))
            return await Task.FromResult(("1", "Supervisor"));
        
        return await Task.FromResult(("", ""));
    }
}
