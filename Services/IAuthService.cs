using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public interface IAuthService
{
    Task<string> GenerateTokenAsync(User user);
    Task<string> GenerateTokenAsync(Agent agent);
    Task<string> GenerateTokenAsync(Supervisor supervisor);
    Task<bool> ValidateTokenAsync(string token);
    Task<(string userId, string userType)> GetTokenInfoAsync(string token);
}
