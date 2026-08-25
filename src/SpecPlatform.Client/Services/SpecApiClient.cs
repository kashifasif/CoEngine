using System.Net.Http.Json;
using System.Text;
using SpecPlatform.Shared.DTOs;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Client.Services;

public class SpecApiClient
{
    private readonly HttpClient _http;
    public string? AuthToken { get; private set; }

    public SpecApiClient(HttpClient http)
    {
        _http = http;
    }

    public void SetAuthToken(string? token)
    {
        AuthToken = token;
        _http.DefaultRequestHeaders.Remove("X-Auth-Token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Add("X-Auth-Token", token);
        }
    }

    public async Task<HealthCheckResponse?> GetHealthAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<HealthCheckResponse>("api/health");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<ProjectDto>> GetProjectsAsync()
    {
        return await _http.GetFromJsonAsync<List<ProjectDto>>("api/projects") ?? new();
    }

    public async Task<ProjectDto?> GetProjectAsync(int id)
    {
        return await _http.GetFromJsonAsync<ProjectDto>($"api/projects/{id}");
    }

    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/projects", dto);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProjectDto>();
        }
        return null;
    }

    public async Task<bool> DeleteProjectAsync(int projectId)
    {
        var response = await _http.DeleteAsync($"api/projects/{projectId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteSpecAsync(int specId)
    {
        var response = await _http.DeleteAsync($"api/specs/{specId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SpecDto>> GetSpecsForProjectAsync(int projectId)
    {
        return await _http.GetFromJsonAsync<List<SpecDto>>($"api/projects/{projectId}/specs") ?? new();
    }

    public async Task<SpecDto?> GetSpecAsync(int id)
    {
        return await _http.GetFromJsonAsync<SpecDto>($"api/specs/{id}");
    }

    public async Task<SpecDto?> CreateSpecAsync(int projectId, CreateSpecDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/specs", dto);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<SpecDto?> PublishMasterSpecAsync(int projectId, CreateSpecDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/publish-master-spec", dto);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<SpecDto?> UpdateSpecAsync(int id, UpdateSpecDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/specs/{id}", dto);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<PublishResultDto?> PublishSpecAsync(int id)
    {
        var response = await _http.PostAsync($"api/specs/{id}/publish", null);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PublishResultDto>();
        }
        return null;
    }

    public async Task<SpecDto?> UndoPublishSpecAsync(int id, int versionNumber)
    {
        var response = await _http.PostAsync($"api/specs/{id}/versions/{versionNumber}/undo", null);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<SpecDto?> RedoPublishSpecAsync(int id, int versionNumber)
    {
        var response = await _http.PostAsync($"api/specs/{id}/versions/{versionNumber}/redo", null);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(int skip = 0, int take = 10)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<NotificationDto>>($"api/notifications?skip={skip}&take={take}") ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<UserAiUsageSummaryDto?> GetUserAiUsageAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<UserAiUsageSummaryDto>("api/users/me/ai-usage");
        }
        catch
        {
            return null;
        }
    }

    public async Task<SpecDiffDto?> CompareVersionsAsync(int specId, int v1, int v2)
    {
        return await _http.GetFromJsonAsync<SpecDiffDto>($"api/specs/{specId}/versions/{v1}/diff/{v2}");
    }

    public async Task<VectorStoreStatsDto?> GetVectorStoreStatsAsync(int projectId)
    {
        try
        {
            return await _http.GetFromJsonAsync<VectorStoreStatsDto>($"api/projects/{projectId}/vector-store");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ChatSessionDto?> GetChatSessionAsync(int projectId, string personaMode, int? skip = null, int? take = null)
    {
        try
        {
            var url = $"api/projects/{projectId}/chat-session/{personaMode}";
            if (take.HasValue)
            {
                url += $"?skip={skip ?? 0}&take={take.Value}";
            }
            return await _http.GetFromJsonAsync<ChatSessionDto>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveChatMessagesAsync(int projectId, string personaMode, List<ChatMessageDto> messages)
    {
        try
        {
            await _http.PostAsJsonAsync($"api/projects/{projectId}/chat-session/{personaMode}/messages", messages);
        }
        catch { }
    }

    public async Task ClearChatSessionAsync(int projectId, string personaMode)
    {
        try
        {
            await _http.DeleteAsync($"api/projects/{projectId}/chat-session/{personaMode}");
        }
        catch { }
    }

    public async Task<ChatResponseDto> SendChatAsync(int projectId, List<ChatMessageDto> messages)
    {
        var request = new ChatRequestDto { ProjectId = projectId, Messages = messages };
        var response = await _http.PostAsJsonAsync("api/specs/draft/chat", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ChatResponseDto>() ?? new ChatResponseDto { Success = false, ErrorMessage = "Empty response" };
        }
        return new ChatResponseDto { Success = false, ErrorMessage = $"HTTP {response.StatusCode}" };
    }

    public async Task SendChatStreamAsync(
        int projectId,
        List<ChatMessageDto> messages,
        Action<string> onChunkReceived,
        bool isClarificationPhase = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ChatRequestDto
            {
                ProjectId = projectId,
                IsClarificationPhase = isClarificationPhase,
                Messages = messages
            };
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/specs/draft/chat/stream")
            {
                Content = JsonContent.Create(request)
            };

            using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                onChunkReceived($"[HTTP Error {(int)response.StatusCode}] Unable to reach AI Service.");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var buffer = new char[512];
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0 && !cancellationToken.IsCancellationRequested)
            {
                var textChunk = new string(buffer, 0, read);
                onChunkReceived(textChunk);
            }
        }
        catch (Exception ex)
        {
            onChunkReceived($"\n[HTTP Error 500] {ex.Message}");
        }
    }

    public async Task SendDevQaQueryStreamAsync(
        int projectId,
        string roleMode,
        List<ChatMessageDto> messages,
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DevQaQueryRequestDto { ProjectId = projectId, RoleMode = roleMode, Messages = messages };
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/specs/query/chat/stream")
            {
                Content = JsonContent.Create(request)
            };

            using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                onChunkReceived($"[HTTP Error {(int)response.StatusCode}] Unable to reach Q/A Mode AI Service.");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var buffer = new char[512];
            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0 && !cancellationToken.IsCancellationRequested)
            {
                var textChunk = new string(buffer, 0, read);
                onChunkReceived(textChunk);
            }
        }
        catch (Exception ex)
        {
            onChunkReceived($"\n[HTTP Error 500] {ex.Message}");
        }
    }

    public async Task<StructuredSpecResultDto?> StructureChatAsync(int projectId, List<ChatMessageDto> messages)
    {
        var request = new ChatRequestDto { ProjectId = projectId, Messages = messages };
        var response = await _http.PostAsJsonAsync("api/specs/draft/structure", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StructuredSpecResultDto>();
        }
        return null;
    }

    public async Task<IngestTranscriptResponseDto?> IngestRawTranscriptAsync(int projectId, string sourceTag, string rawTranscript)
    {
        var request = new IngestTranscriptRequestDto
        {
            ProjectId = projectId,
            SourceTag = sourceTag,
            RawTranscript = rawTranscript
        };
        var response = await _http.PostAsJsonAsync("api/specs/draft/ingest-transcript", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<IngestTranscriptResponseDto>();
        }
        return null;
    }

    public async Task<GitHubAuthUrlDto?> GetGitHubAuthUrlAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<GitHubAuthUrlDto>("api/auth/github/url");
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserDto?> ProcessGitHubCallbackAsync(string code)
    {
        try
        {
            var req = new GitHubCallbackRequestDto { Code = code };
            var response = await _http.PostAsJsonAsync("api/auth/github/callback", req);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<UserDto>("api/auth/me");
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _http.PostAsync("api/auth/logout", null);
        }
        catch { }
    }
}
