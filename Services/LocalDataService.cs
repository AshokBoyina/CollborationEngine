using CollaborationEngine.API.Models;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;

namespace CollaborationEngine.API.Services;

public class LocalDataService : ILocalDataService
{
    private readonly string _dataPath;
    private readonly string _recordingsPath;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    public LocalDataService(IWebHostEnvironment environment)
    {
        _dataPath = Path.Combine(environment.ContentRootPath, "Data");
        _recordingsPath = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "recordings");
        
        // Ensure directories exist
        Directory.CreateDirectory(_dataPath);
        Directory.CreateDirectory(_recordingsPath);
    }

    private async Task<List<T>> ReadDataAsync<T>(string fileName)
    {
        var filePath = Path.Combine(_dataPath, fileName);
        if (!File.Exists(filePath))
        {
            return new List<T>();
        }

        var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new List<T>();
                    }

                    return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(25 * (attempt + 1));
                }
                catch (JsonException) when (attempt < 4)
                {
                    await Task.Delay(25 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to read/parse data file: {fileName}", ex);
                }
            }

            throw new InvalidOperationException($"Failed to read/parse data file after retries: {fileName}");
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task SaveDataAsync<T>(string fileName, List<T> data)
    {
        var filePath = Path.Combine(_dataPath, fileName);
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);

        var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync();
        try
        {
            var tempPath = filePath + ".tmp";

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    await File.WriteAllTextAsync(tempPath, json);

                    // Atomic-ish replace on the same volume.
                    // File.Move with overwrite avoids partial reads that can happen with Copy+Delete.
                    File.Move(tempPath, filePath, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 4)
                {
                    await Task.Delay(25 * (attempt + 1));
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    await Task.Delay(25 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to save data file: {fileName}", ex);
                }
            }

            throw new IOException($"Failed to save data file after retries: {fileName}");
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<List<Application>> GetApplicationsAsync()
    {
        return await ReadDataAsync<Application>("applications.json");
    }

    public async Task<Application?> GetApplicationAsync(int id)
    {
        var apps = await GetApplicationsAsync();
        return apps.FirstOrDefault(a => a.Id == id);
    }

    public async Task<Application> SaveApplicationAsync(Application application)
    {
        var apps = await GetApplicationsAsync();
        
        if (application.Id == 0)
        {
            application.Id = apps.Any() ? apps.Max(a => a.Id) + 1 : 1;
            application.CreatedAt = DateTime.UtcNow;
            apps.Add(application);
        }
        else
        {
            var existing = apps.FirstOrDefault(a => a.Id == application.Id);
            if (existing != null)
            {
                existing.Name = application.Name;
                existing.Description = application.Description;
                existing.ApiKey = application.ApiKey;
                existing.IsActive = application.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await SaveDataAsync("applications.json", apps);
        return application;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        return await ReadDataAsync<User>("users.json");
    }

    public async Task<User?> GetUserAsync(int id)
    {
        var users = await GetUsersAsync();
        return users.FirstOrDefault(u => u.Id == id);
    }

    public async Task<User> SaveUserAsync(User user)
    {
        var users = await GetUsersAsync();
        
        if (user.Id == 0)
        {
            user.Id = users.Any() ? users.Max(u => u.Id) + 1 : 1;
            user.CreatedAt = DateTime.UtcNow;
            users.Add(user);
        }
        else
        {
            var existing = users.FirstOrDefault(u => u.Id == user.Id);
            if (existing != null)
            {
                existing.Name = user.Name;
                existing.Email = user.Email;
                existing.PhoneNumber = user.PhoneNumber;
                existing.AdditionalDetails = user.AdditionalDetails;
                existing.IsActive = user.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await SaveDataAsync("users.json", users);
        return user;
    }

    public async Task<List<Agent>> GetAgentsAsync()
    {
        return await ReadDataAsync<Agent>("agents.json");
    }

    public async Task<Agent?> GetAgentAsync(int id)
    {
        var agents = await GetAgentsAsync();
        return agents.FirstOrDefault(a => a.Id == id);
    }

    public async Task<Agent> SaveAgentAsync(Agent agent)
    {
        var agents = await GetAgentsAsync();
        
        if (agent.Id == 0)
        {
            agent.Id = agents.Any() ? agents.Max(a => a.Id) + 1 : 1;
            agent.CreatedAt = DateTime.UtcNow;
            agents.Add(agent);
        }
        else
        {
            var existing = agents.FirstOrDefault(a => a.Id == agent.Id);
            if (existing != null)
            {
                existing.Name = agent.Name;
                existing.Email = agent.Email;
                existing.PhoneNumber = agent.PhoneNumber;
                existing.Department = agent.Department;
                existing.IsAvailable = agent.IsAvailable;
                existing.IsActive = agent.IsActive;
                existing.LastActiveAt = agent.LastActiveAt;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await SaveDataAsync("agents.json", agents);
        return agent;
    }

    public async Task<List<Supervisor>> GetSupervisorsAsync()
    {
        return await ReadDataAsync<Supervisor>("supervisors.json");
    }

    public async Task<Supervisor?> GetSupervisorAsync(int id)
    {
        var supervisors = await GetSupervisorsAsync();
        return supervisors.FirstOrDefault(s => s.Id == id);
    }

    public async Task<Supervisor> SaveSupervisorAsync(Supervisor supervisor)
    {
        var supervisors = await GetSupervisorsAsync();
        
        if (supervisor.Id == 0)
        {
            supervisor.Id = supervisors.Any() ? supervisors.Max(s => s.Id) + 1 : 1;
            supervisor.CreatedAt = DateTime.UtcNow;
            supervisors.Add(supervisor);
        }
        else
        {
            var existing = supervisors.FirstOrDefault(s => s.Id == supervisor.Id);
            if (existing != null)
            {
                existing.Name = supervisor.Name;
                existing.Email = supervisor.Email;
                existing.PhoneNumber = supervisor.PhoneNumber;
                existing.Department = supervisor.Department;
                existing.IsAvailable = supervisor.IsAvailable;
                existing.IsActive = supervisor.IsActive;
                existing.LastActiveAt = supervisor.LastActiveAt;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await SaveDataAsync("supervisors.json", supervisors);
        return supervisor;
    }

    public async Task<List<CollaborationSession>> GetCollaborationSessionsAsync()
    {
        return await ReadDataAsync<CollaborationSession>("collaborationSessions.json");
    }

    public async Task<CollaborationSession?> GetCollaborationSessionAsync(string collaborationId)
    {
        if (string.IsNullOrWhiteSpace(collaborationId))
        {
            return null;
        }

        var normalized = collaborationId.Trim();
        var sessions = await GetCollaborationSessionsAsync();
        return sessions.FirstOrDefault(s =>
            !string.IsNullOrWhiteSpace(s.CollaborationId) &&
            string.Equals(s.CollaborationId.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CollaborationSession> SaveCollaborationSessionAsync(CollaborationSession session)
    {
        // IMPORTANT: single locked read-modify-write to avoid lost updates under concurrency.
        var fileName = "collaborationSessions.json";
        var filePath = Path.Combine(_dataPath, fileName);
        var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync();
        try
        {
            List<CollaborationSession> sessions;
            if (!File.Exists(filePath))
            {
                sessions = new List<CollaborationSession>();
            }
            else
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();
                sessions = string.IsNullOrWhiteSpace(json)
                    ? new List<CollaborationSession>()
                    : (JsonConvert.DeserializeObject<List<CollaborationSession>>(json) ?? new List<CollaborationSession>());
            }

            if (session.Id == 0)
            {
                session.Id = sessions.Any() ? sessions.Max(s => s.Id) + 1 : 1;
                sessions.Add(session);
            }
            else
            {
                var existing = sessions.FirstOrDefault(s => s.Id == session.Id);
                if (existing != null)
                {
                    existing.CollaborationId = session.CollaborationId;
                    existing.Status = session.Status;
                    existing.AgentId = session.AgentId;
                    existing.EndedAt = session.EndedAt;
                    existing.RecordingUrl = session.RecordingUrl;
                    existing.IsRecording = session.IsRecording;
                    existing.StreamUrl = session.StreamUrl;
                    existing.UpdatedAt = session.UpdatedAt;
                    existing.StartedAt = session.StartedAt;
                    existing.UserId = session.UserId;
                    existing.ApplicationId = session.ApplicationId;
                }
                else
                {
                    sessions.Add(session);
                }
            }

            var tempPath = filePath + ".tmp";
            var outJson = JsonConvert.SerializeObject(sessions, Formatting.Indented);
            await File.WriteAllTextAsync(tempPath, outJson);
            File.Move(tempPath, filePath, overwrite: true);

            return session;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(int sessionId)
    {
        var allMessages = await ReadDataAsync<ChatMessage>("chatMessages.json");
        return allMessages.Where(m => m.CollaborationSessionId == sessionId).ToList();
    }

    public async Task<ChatMessage> SaveChatMessageAsync(ChatMessage message)
    {
        var messages = await ReadDataAsync<ChatMessage>("chatMessages.json");
        
        if (message.Id == 0)
        {
            message.Id = messages.Any() ? messages.Max(m => m.Id) + 1 : 1;
            messages.Add(message);
        }
        
        await SaveDataAsync("chatMessages.json", messages);
        return message;
    }

    public async Task<List<AgentSupervisorSession>> GetAgentSupervisorSessionsAsync()
    {
        return await ReadDataAsync<AgentSupervisorSession>("agentSupervisorSessions.json");
    }

    public async Task<AgentSupervisorSession> SaveAgentSupervisorSessionAsync(AgentSupervisorSession session)
    {
        var sessions = await GetAgentSupervisorSessionsAsync();
        
        if (session.Id == 0)
        {
            session.Id = sessions.Any() ? sessions.Max(s => s.Id) + 1 : 1;
            sessions.Add(session);
        }
        
        await SaveDataAsync("agentSupervisorSessions.json", sessions);
        return session;
    }
}
