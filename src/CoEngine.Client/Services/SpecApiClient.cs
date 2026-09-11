using System.Net.Http.Json;
using System.Text;
using CoEngine.Shared.DTOs;
using CoEngine.Shared.Models;
using CoEngine.Client.Exceptions;

using Microsoft.JSInterop;

namespace CoEngine.Client.Services;

public class SpecApiClient
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly CopilotAiClient _copilot;

    public SpecApiClient(HttpClient http, IJSRuntime js, CopilotAiClient copilot)
    {
        _http = http;
        _js = js;
        _copilot = copilot;
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

    public async Task<ProjectDto?> GetProjectAsync(Guid id)
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

    public async Task<bool> DeleteProjectAsync(Guid projectId)
    {
        var response = await _http.DeleteAsync($"api/projects/{projectId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteSpecAsync(Guid specId)
    {
        var response = await _http.DeleteAsync($"api/specs/{specId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SpecDto>> GetSpecsForProjectAsync(Guid projectId)
    {
        return await _http.GetFromJsonAsync<List<SpecDto>>($"api/projects/{projectId}/specs") ?? new();
    }

    public async Task<SpecDto?> GetSpecAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<SpecDto>($"api/specs/{id}");
    }

    public async Task<SpecDto?> CreateSpecAsync(Guid projectId, CreateSpecDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/specs", dto);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<SpecDto?> PublishMasterSpecAsync(Guid projectId, CreateSpecDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/projects/{projectId}/publish-master-spec", dto);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<SpecDto?> UpdateSpecAsync(Guid id, UpdateSpecDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/specs/{id}", dto);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new ConcurrencyConflictException(error?.Message ?? "This spec was updated by someone else.");
        }
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    private class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    public async Task<PublishResultDto?> PublishSpecAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/specs/{id}/publish", null);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PublishResultDto>();
        }
        return null;
    }

    public async Task<SpecDto?> UndoPublishSpecAsync(Guid id, int versionNumber)
    {
        var response = await _http.PostAsync($"api/specs/{id}/versions/{versionNumber}/undo", null);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SpecDto>();
        }
        return null;
    }

    public async Task<SpecDto?> RedoPublishSpecAsync(Guid id, int versionNumber)
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

    public async Task<SpecDiffDto?> CompareVersionsAsync(Guid specId, int v1, int v2)
    {
        return await _http.GetFromJsonAsync<SpecDiffDto>($"api/specs/{specId}/versions/{v1}/diff/{v2}");
    }

    public async Task<VectorStoreStatsDto?> GetVectorStoreStatsAsync(Guid projectId)
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

    public async Task<ChatSessionDto?> GetChatSessionAsync(Guid projectId, string personaMode, int? skip = null, int? take = null)
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

    public async Task SaveChatMessagesAsync(Guid projectId, string personaMode, List<ChatMessageDto> messages)
    {
        try
        {
            await _http.PostAsJsonAsync($"api/projects/{projectId}/chat-session/{personaMode}/messages", messages);
        }
        catch { }
    }

    public async Task ClearChatSessionAsync(Guid projectId, string personaMode)
    {
        try
        {
            await _http.DeleteAsync($"api/projects/{projectId}/chat-session/{personaMode}");
        }
        catch { }
    }

    public async Task<ChatResponseDto> SendChatAsync(Guid projectId, List<ChatMessageDto> messages)
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
        Guid projectId,
        List<ChatMessageDto> messages,
        Action<string> onChunkReceived,
        bool isClarificationPhase = false,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatRequestDto
        {
            ProjectId = projectId,
            IsClarificationPhase = isClarificationPhase,
            Messages = messages
        };

        // 1. Try local Copilot bridge if available
        if (await _copilot.IsCopilotAvailableAsync())
        {
            try
            {
                var ctxResponse = await _http.PostAsJsonAsync("api/specs/draft/chat/context", request, cancellationToken);
                if (ctxResponse.IsSuccessStatusCode)
                {
                    var ctx = await ctxResponse.Content.ReadFromJsonAsync<BrainstormContextResponseDto>(cancellationToken: cancellationToken);
                    if (ctx != null && !string.IsNullOrWhiteSpace(ctx.SystemPrompt))
                    {
                        var copilotSuccess = await _copilot.StreamChatCompletionAsync(
                            ctx.SystemPrompt,
                            messages,
                            onChunkReceived,
                            cancellationToken);

                        if (copilotSuccess) return;
                    }
                }
            }
            catch (Exception ex)
            {
                await _js.InvokeVoidAsync("console.warn", $"[SpecApiClient] Local Copilot error, falling back to server: {ex.Message}");
            }
        }

        // 2. Fallback to server stream
        try
        {
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
        Guid projectId,
        string roleMode,
        List<ChatMessageDto> messages,
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default)
    {
        var request = new DevQaQueryRequestDto { ProjectId = projectId, RoleMode = roleMode, Messages = messages };

        // 1. Try local Copilot bridge if available (uses remote pgvector search context)
        if (await _copilot.IsCopilotAvailableAsync())
        {
            try
            {
                var ctxResponse = await _http.PostAsJsonAsync("api/specs/query/context", request, cancellationToken);
                if (ctxResponse.IsSuccessStatusCode)
                {
                    var ctx = await ctxResponse.Content.ReadFromJsonAsync<DevQaContextResponseDto>(cancellationToken: cancellationToken);
                    if (ctx != null && !string.IsNullOrWhiteSpace(ctx.SystemPrompt))
                    {
                        var copilotSuccess = await _copilot.StreamChatCompletionAsync(
                            ctx.SystemPrompt,
                            messages,
                            onChunkReceived,
                            cancellationToken);

                        if (copilotSuccess) return;
                    }
                }
            }
            catch (Exception ex)
            {
                await _js.InvokeVoidAsync("console.warn", $"[SpecApiClient] Local Copilot error in Q/A, falling back to server: {ex.Message}");
            }
        }

        // 2. Fallback to server stream
        try
        {
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

    public async Task<StructuredSpecResultDto?> StructureChatAsync(Guid projectId, List<ChatMessageDto> messages)
    {
        var request = new ChatRequestDto { ProjectId = projectId, Messages = messages };

        // 1. Try local Copilot bridge if available
        if (await _copilot.IsCopilotAvailableAsync())
        {
            try
            {
                var ctxRes = await _http.PostAsJsonAsync("api/specs/draft/structure/context", request);
                var selfReviewPromptRes = await _http.GetAsync("api/specs/prompts/self-review");

                if (ctxRes.IsSuccessStatusCode)
                {
                    var ctx = await ctxRes.Content.ReadFromJsonAsync<BrainstormContextResponseDto>();
                    var selfReviewPrompt = selfReviewPromptRes.IsSuccessStatusCode ? await selfReviewPromptRes.Content.ReadAsStringAsync() : "";

                    if (ctx != null && !string.IsNullOrWhiteSpace(ctx.SystemPrompt))
                    {
                        var structured = await _copilot.StructureSpecAsync(ctx.SystemPrompt, messages, selfReviewPrompt);
                        if (structured != null) return structured;
                    }
                }
            }
            catch (Exception ex)
            {
                await _js.InvokeVoidAsync("console.warn", $"[SpecApiClient] Local Copilot structuring error, falling back to server: {ex.Message}");
            }
        }

        // 2. Fallback to server structuring
        var response = await _http.PostAsJsonAsync("api/specs/draft/structure", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StructuredSpecResultDto>();
        }
        return null;
    }

    public async Task<IngestTranscriptResponseDto?> IngestRawTranscriptAsync(Guid projectId, string sourceTag, string rawTranscript)
    {
        StructuredSpecResultDto? preStructured = null;
        if (await _copilot.IsCopilotAvailableAsync())
        {
            try
            {
                var ctxRes = await _http.PostAsJsonAsync("api/specs/draft/ingest-transcript/context", new IngestTranscriptRequestDto
                {
                    ProjectId = projectId,
                    SourceTag = sourceTag,
                    RawTranscript = rawTranscript
                });

                if (ctxRes.IsSuccessStatusCode)
                {
                    var ctx = await ctxRes.Content.ReadFromJsonAsync<BrainstormContextResponseDto>();
                    if (ctx != null && !string.IsNullOrWhiteSpace(ctx.SystemPrompt))
                    {
                        var messages = new List<ChatMessageDto>
                        {
                            new()
                            {
                                Role = "user",
                                Content = $"Here is the raw meeting transcript / requirements dump ({sourceTag}):\n\n```\n{rawTranscript}\n```\n\nPlease deeply analyze this text, filter out noise/banter, extract the core technical requirements, actors, acceptance criteria, and edge cases."
                            }
                        };
                        preStructured = await _copilot.StructureSpecAsync(ctx.SystemPrompt, messages, "");
                    }
                }
            }
            catch (Exception ex)
            {
                await _js.InvokeVoidAsync("console.warn", $"[SpecApiClient] Copilot transcript ingestion error, falling back to server: {ex.Message}");
            }
        }

        var request = new IngestTranscriptRequestDto
        {
            ProjectId = projectId,
            SourceTag = sourceTag,
            RawTranscript = rawTranscript,
            PreStructuredResult = preStructured
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

    public async Task<UserDto?> DevLoginAsync(Guid? userId = null)
    {
        try
        {
            var url = userId.HasValue ? $"api/auth/dev-login?userId={userId.Value}" : "api/auth/dev-login";
            var response = await _http.PostAsync(url, null);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<UserDto>();
                if (user != null && user.IsAuthenticated)
                {
                    _cachedUser = user;
                    if (!string.IsNullOrWhiteSpace(user.Token))
                    {
                        try { await _js.InvokeVoidAsync("localStorage.setItem", "spec_user_session", user.Token); } catch { }
                    }
                }
                return user;
            }
            return null;
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
                var user = await response.Content.ReadFromJsonAsync<UserDto>();
                if (user != null && !string.IsNullOrWhiteSpace(user.Token))
                {
                    try { await _js.InvokeVoidAsync("localStorage.setItem", "spec_user_session", user.Token); } catch { }
                }
                return user;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private UserDto? _cachedUser;

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        if (_cachedUser != null) return _cachedUser;
        try
        {
            var user = await _http.GetFromJsonAsync<UserDto>("api/auth/me");
            if (user != null && user.IsAuthenticated)
            {
                _cachedUser = user;
            }
            return user;
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        _cachedUser = null;
        try
        {
            try { await _js.InvokeVoidAsync("localStorage.removeItem", "spec_user_session"); } catch { }
            await _http.PostAsync("api/auth/logout", null);
        }
        catch { }
    }
}
