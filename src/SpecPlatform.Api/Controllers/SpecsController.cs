using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpecPlatform.Api.Data;
using SpecPlatform.Api.Services;
using SpecPlatform.Shared.DTOs;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Controllers;

[ApiController]
public class SpecsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOpenRouterService _openRouter;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILogger<SpecsController> _logger;

    public SpecsController(AppDbContext db, IOpenRouterService openRouter, IVectorStoreService vectorStore, ILogger<SpecsController> logger)
    {
        _db = db;
        _openRouter = openRouter;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    [HttpGet("api/projects/{projectId:int}/specs")]
    public async Task<ActionResult<List<SpecDto>>> GetSpecsForProject(int projectId)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return NotFound(new { message = $"Project {projectId} not found." });
        }

        var specs = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
                .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
                .ThenInclude(v => v.ScopeTags)
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(specs.Select(MapToSpecDto).ToList());
    }

    [HttpPost("api/projects/{projectId:int}/specs")]
    public async Task<ActionResult<SpecDto>> CreateSpec(int projectId, [FromBody] CreateSpecDto dto)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null)
        {
            return NotFound(new { message = $"Project {projectId} not found." });
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { message = "Spec Title is required." });
        }

        var spec = new Spec
        {
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        _db.Specs.Add(spec);
        await _db.SaveChangesAsync();

        var draftVersion = new SpecVersion
        {
            SpecId = spec.Id,
            VersionNumber = 0,
            Content = spec.Description,
            PublishedAt = DateTime.UtcNow,
            AcceptanceCriteria = dto.AcceptanceCriteria
                .Where(ac => !string.IsNullOrWhiteSpace(ac))
                .Select(ac => new AcceptanceCriterion { Text = ac.Trim() })
                .ToList(),
            ScopeTags = dto.ScopeTags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => new ScopeTag { TagName = t.Trim() })
                .ToList()
        };

        _db.SpecVersions.Add(draftVersion);
        await _db.SaveChangesAsync();

        // Index Draft into Vector Store
        var criteriaText = string.Join(". ", dto.AcceptanceCriteria);
        var tagsText = string.Join(", ", dto.ScopeTags);
        await _vectorStore.IndexDocumentAsync(projectId, "spec", spec.Title, $"{spec.Description}. Acceptance criteria: {criteriaText}. Scope tags: {tagsText}");

        spec.Project = project;
        return CreatedAtAction(nameof(GetSpec), new { id = spec.Id }, MapToSpecDto(spec));
    }

    [HttpGet("api/specs/{id:int}")]
    public async Task<ActionResult<SpecDto>> GetSpec(int id)
    {
        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
                .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
                .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (spec == null)
        {
            return NotFound(new { message = $"Spec {id} not found." });
        }

        return Ok(MapToSpecDto(spec));
    }

    [HttpPut("api/specs/{id:int}")]
    public async Task<ActionResult<SpecDto>> UpdateDraftSpec(int id, [FromBody] UpdateSpecDto dto)
    {
        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
                .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
                .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (spec == null)
        {
            return NotFound(new { message = $"Spec {id} not found." });
        }

        spec.Title = dto.Title.Trim();
        spec.Description = dto.Description?.Trim() ?? string.Empty;

        var draftVersion = spec.Versions.FirstOrDefault(v => v.VersionNumber == 0);
        if (draftVersion == null)
        {
            var latestPublished = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            draftVersion = new SpecVersion
            {
                SpecId = spec.Id,
                VersionNumber = 0,
                Content = spec.Description,
                PublishedAt = DateTime.UtcNow,
                AcceptanceCriteria = (latestPublished?.AcceptanceCriteria ?? new List<AcceptanceCriterion>())
                    .Select(ac => new AcceptanceCriterion { Text = ac.Text }).ToList(),
                ScopeTags = (latestPublished?.ScopeTags ?? new List<ScopeTag>())
                    .Select(st => new ScopeTag { TagName = st.TagName }).ToList()
            };
            _db.SpecVersions.Add(draftVersion);
        }

        draftVersion.Content = spec.Description;
        draftVersion.AcceptanceCriteria.Clear();
        foreach (var ac in dto.AcceptanceCriteria.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            draftVersion.AcceptanceCriteria.Add(new AcceptanceCriterion { Text = ac.Trim() });
        }

        draftVersion.ScopeTags.Clear();
        foreach (var tag in dto.ScopeTags.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            draftVersion.ScopeTags.Add(new ScopeTag { TagName = tag.Trim() });
        }

        await _db.SaveChangesAsync();

        // Index Updated Spec into Vector Store
        var criteriaText = string.Join(". ", dto.AcceptanceCriteria);
        var tagsText = string.Join(", ", dto.ScopeTags);
        await _vectorStore.IndexDocumentAsync(spec.ProjectId, "spec", spec.Title, $"{spec.Description}. Acceptance criteria: {criteriaText}. Scope tags: {tagsText}");

        return Ok(MapToSpecDto(spec));
    }

    [HttpPost("api/specs/{id:int}/publish")]
    public async Task<ActionResult<PublishResultDto>> PublishSpec(int id)
    {
        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
                .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
                .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (spec == null)
        {
            return NotFound(new { message = $"Spec {id} not found." });
        }

        var publishedVersions = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber).ToList();
        int nextVersionNumber = publishedVersions.Any() ? publishedVersions.First().VersionNumber + 1 : 1;

        var draftVersion = spec.Versions.FirstOrDefault(v => v.VersionNumber == 0)
                            ?? spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        var newPublishedVersion = new SpecVersion
        {
            SpecId = spec.Id,
            VersionNumber = nextVersionNumber,
            Content = spec.Description,
            PublishedAt = DateTime.UtcNow,
            AcceptanceCriteria = (draftVersion?.AcceptanceCriteria ?? new List<AcceptanceCriterion>())
                .Select(ac => new AcceptanceCriterion { Text = ac.Text })
                .ToList(),
            ScopeTags = (draftVersion?.ScopeTags ?? new List<ScopeTag>())
                .Select(st => new ScopeTag { TagName = st.TagName })
                .ToList()
        };

        _db.SpecVersions.Add(newPublishedVersion);
        spec.Status = "Published";
        await _db.SaveChangesAsync();

        var criteriaCount = newPublishedVersion.AcceptanceCriteria.Count;
        var tagsCount = newPublishedVersion.ScopeTags.Count;
        var summaryText = $"[NOTIFICATION] Spec '{spec.Title}' in project '{spec.Project?.Name}' published as Version {nextVersionNumber}. Contains {criteriaCount} acceptance criteria and {tagsCount} scope tags.";

        var notification = new Notification
        {
            SpecId = spec.Id,
            SpecTitle = spec.Title,
            ProjectName = spec.Project?.Name ?? "General",
            VersionNumber = nextVersionNumber,
            SummaryText = summaryText,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        // Index Published Version into Vector Store
        var pubCriteria = string.Join(". ", newPublishedVersion.AcceptanceCriteria.Select(a => a.Text));
        var pubTags = string.Join(", ", newPublishedVersion.ScopeTags.Select(t => t.TagName));
        await _vectorStore.IndexDocumentAsync(spec.ProjectId, "spec", $"{spec.Title} (v{nextVersionNumber})", $"{spec.Description}. Acceptance criteria: {pubCriteria}. Scope tags: {pubTags}");

        _logger.LogInformation(summaryText);

        return Ok(new PublishResultDto
        {
            Spec = MapToSpecDto(spec),
            NotificationSummary = summaryText
        });
    }

    [HttpGet("api/notifications")]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications()
    {
        var notifications = await _db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                SpecId = n.SpecId,
                SpecTitle = n.SpecTitle,
                ProjectName = n.ProjectName,
                VersionNumber = n.VersionNumber,
                SummaryText = n.SummaryText,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("api/specs/{id:int}/versions/{v1:int}/diff/{v2:int}")]
    public async Task<ActionResult<SpecDiffDto>> CompareVersions(int id, int v1, int v2)
    {
        var spec = await _db.Specs
            .Include(s => s.Versions)
                .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
                .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (spec == null) return NotFound();

        var ver1 = spec.Versions.FirstOrDefault(v => v.VersionNumber == v1);
        var ver2 = spec.Versions.FirstOrDefault(v => v.VersionNumber == v2);

        if (ver1 == null || ver2 == null) return BadRequest("One or both specified version numbers do not exist.");

        var c1 = ver1.AcceptanceCriteria.Select(ac => ac.Text).ToHashSet();
        var c2 = ver2.AcceptanceCriteria.Select(ac => ac.Text).ToHashSet();

        var t1 = ver1.ScopeTags.Select(st => st.TagName).ToHashSet();
        var t2 = ver2.ScopeTags.Select(st => st.TagName).ToHashSet();

        var addedCriteria = c2.Except(c1).ToList();
        var removedCriteria = c1.Except(c2).ToList();
        var addedTags = t2.Except(t1).ToList();
        var removedTags = t1.Except(t2).ToList();

        var summary = $"Version {v1} -> {v2}: {addedCriteria.Count} criteria added, {removedCriteria.Count} criteria removed, {addedTags.Count} tags added, {removedTags.Count} tags removed.";

        return Ok(new SpecDiffDto
        {
            SpecId = id,
            FromVersion = v1,
            ToVersion = v2,
            AddedCriteria = addedCriteria,
            RemovedCriteria = removedCriteria,
            AddedScopeTags = addedTags,
            RemovedScopeTags = removedTags,
            AiSummary = summary
        });
    }

    [HttpGet("api/projects/{projectId:int}/vector-store")]
    public async Task<ActionResult<VectorStoreStatsDto>> GetVectorStoreStats(int projectId)
    {
        var stats = await _vectorStore.GetStatsAsync(projectId);
        return Ok(stats);
    }

    [HttpPost("api/specs/draft/chat")]
    public async Task<ActionResult<ChatResponseDto>> BrainstormChat([FromBody] ChatRequestDto request)
    {
        var project = await _db.Projects.FindAsync(request.ProjectId);
        var projectName = project?.Name ?? "General";
        var projectDesc = project?.Description ?? "Requirements brainstorming";

        var systemPrompt = $"You are an expert Agile Product Owner & Business Analyst AI assistant.\n" +
                           $"Context: You are helping draft a technical requirement/spec for Project: '{projectName}'.\n" +
                           $"Project Overview: {projectDesc}\n" +
                           $"Goal: Help the user define title, user stories, acceptance criteria, and technical scope tags (e.g. 'api', 'bff', 'mfe', 'db'). Ask clarifying questions if needed.";

        var response = await _openRouter.ChatAsync(systemPrompt, request.Messages);
        return Ok(response);
    }

    // --- DB Chat Session Persistence & Vector Store Endpoints ---

    [HttpGet("api/projects/{projectId:int}/chat-session/{personaMode}")]
    public async Task<ActionResult<ChatSessionDto>> GetOrCreateChatSession(int projectId, string personaMode)
    {
        var session = await _db.ChatSessions
            .Include(cs => cs.Messages)
            .FirstOrDefaultAsync(cs => cs.ProjectId == projectId && cs.PersonaMode == personaMode);

        if (session == null)
        {
            session = new ChatSession
            {
                ProjectId = projectId,
                PersonaMode = personaMode,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync();
        }

        return Ok(new ChatSessionDto
        {
            Id = session.Id,
            ProjectId = session.ProjectId,
            PersonaMode = session.PersonaMode,
            UpdatedAt = session.UpdatedAt,
            Messages = session.Messages.OrderBy(m => m.Timestamp).Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        });
    }

    [HttpPost("api/projects/{projectId:int}/chat-session/{personaMode}/messages")]
    public async Task<IActionResult> SaveChatMessages(int projectId, string personaMode, [FromBody] List<ChatMessageDto> newMessages)
    {
        var session = await _db.ChatSessions
            .Include(cs => cs.Messages)
            .FirstOrDefaultAsync(cs => cs.ProjectId == projectId && cs.PersonaMode == personaMode);

        if (session == null)
        {
            session = new ChatSession
            {
                ProjectId = projectId,
                PersonaMode = personaMode,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync();
        }

        session.Messages.Clear();
        foreach (var msg in newMessages)
        {
            session.Messages.Add(new ChatMessageRecord
            {
                ChatSessionId = session.Id,
                Role = msg.Role,
                Content = msg.Content,
                Timestamp = DateTime.UtcNow
            });
        }
        session.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Index Chat Transcript into Vector Store DB
        var transcript = string.Join(" | ", newMessages.Select(m => $"{m.Role}: {m.Content}"));
        await _vectorStore.IndexDocumentAsync(projectId, "chat", $"Chat Memory ({personaMode})", transcript);

        return Ok(new { success = true });
    }

    [HttpDelete("api/projects/{projectId:int}/chat-session/{personaMode}")]
    public async Task<IActionResult> ClearChatSession(int projectId, string personaMode)
    {
        var session = await _db.ChatSessions
            .Include(cs => cs.Messages)
            .FirstOrDefaultAsync(cs => cs.ProjectId == projectId && cs.PersonaMode == personaMode);

        if (session != null)
        {
            _db.ChatSessions.Remove(session);
            await _db.SaveChangesAsync();
        }

        return Ok(new { success = true });
    }

    [HttpPost("api/specs/draft/chat/stream")]
    public async Task BrainstormChatStream(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";

        var project = await _db.Projects.FindAsync(new object[] { request.ProjectId }, cancellationToken);
        var projectName = project?.Name ?? "General";
        var projectDesc = project?.Description ?? "Requirements brainstorming";

        var systemPrompt = $"You are an expert Agile Product Owner & Business Analyst AI assistant.\n" +
                           $"Context: You are helping draft a technical requirement/spec for Project: '{projectName}'.\n" +
                           $"Project Overview: {projectDesc}\n" +
                           $"Goal: Help the user define title, user stories, acceptance criteria, and technical scope tags (e.g. 'api', 'bff', 'mfe', 'db'). Ask clarifying questions if needed.";

        await foreach (var chunk in _openRouter.ChatStreamAsync(systemPrompt, request.Messages, cancellationToken))
        {
            await Response.WriteAsync(chunk, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("api/specs/query/chat/stream")]
    public async Task DevQaQueryStream(
        [FromBody] DevQaQueryRequestDto request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";

        var project = await _db.Projects
            .Include(p => p.Specs)
                .ThenInclude(s => s.Versions)
                    .ThenInclude(v => v.AcceptanceCriteria)
            .Include(p => p.Specs)
                .ThenInclude(s => s.Versions)
                    .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        var projectName = project?.Name ?? "General Project";
        var projectDesc = project?.Description ?? "Technical system";

        var userQuery = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";

        // Query Vector Store for top semantically relevant document & chat chunks!
        var vectorMatches = await _vectorStore.SearchSimilarityAsync(request.ProjectId, userQuery, topK: 5);

        var specContext = new StringBuilder();
        specContext.AppendLine($"--- VECTOR STORE KNOWLEDGE BASE (PUBLISHED SPECS & CHAT MEMORY) ---");

        if (vectorMatches.Any())
        {
            foreach (var match in vectorMatches)
            {
                specContext.AppendLine($"\n[Vector Match | Type: {match.DocType.ToUpper()} | Similarity Score: {match.SimilarityScore:F2}]");
                specContext.AppendLine($"Title: {match.Title}");
                specContext.AppendLine($"Content: {match.Content}");
            }
        }
        else if (project?.Specs != null && project.Specs.Any())
        {
            foreach (var spec in project.Specs)
            {
                var latestVer = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                                ?? spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

                specContext.AppendLine($"\nSpec Title: {spec.Title} (Status: {spec.Status}, Version: v{latestVer?.VersionNumber ?? 1})");
                specContext.AppendLine($"Description: {spec.Description}");

                if (latestVer?.AcceptanceCriteria.Any() == true)
                {
                    specContext.AppendLine("Acceptance Criteria:");
                    foreach (var ac in latestVer.AcceptanceCriteria)
                    {
                        specContext.AppendLine($" - {ac.Text}");
                    }
                }
            }
        }

        string roleInstructions = request.RoleMode == "qa"
            ? "You are a Senior QA Test Automation Lead AI Assistant. Help QA Engineers define test scenarios, edge cases, negative test conditions, Gherkin Given-When-Then syntax, and regression test suites based strictly on the Vector DB knowledge base above."
            : "You are a Lead Software Architect & Senior Developer AI Assistant. Help Developers understand technical implementation details, microservice boundaries, API payloads, DB schema impacts, and exception handling based strictly on the Vector DB knowledge base above.";

        var systemPrompt = $"{roleInstructions}\n\nProject Overview: {projectDesc}\n\n{specContext}\n\nGoal: Answer the Dev/QA query accurately based on the Vector Database records.";

        await foreach (var chunk in _openRouter.ChatStreamAsync(systemPrompt, request.Messages, cancellationToken))
        {
            await Response.WriteAsync(chunk, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("api/specs/draft/structure")]
    public async Task<ActionResult<StructuredSpecResultDto>> StructureChat([FromBody] ChatRequestDto request)
    {
        var project = await _db.Projects.FindAsync(request.ProjectId);
        var projectName = project?.Name ?? "General";
        var projectDesc = project?.Description ?? "Requirements brainstorming";

        var systemPrompt = $"You are an expert Agile Product Owner.\n" +
                           $"Project Context: '{projectName}' - {projectDesc}.";

        var result = await _openRouter.StructureIntoSpecAsync(systemPrompt, request.Messages);
        return Ok(result);
    }

    private static SpecDto MapToSpecDto(Spec spec)
    {
        var publishedVersions = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber).ToList();
        var currentVersion = publishedVersions.FirstOrDefault() ?? spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        int currentVersionNumber = publishedVersions.Any() ? publishedVersions.First().VersionNumber : 1;

        return new SpecDto
        {
            Id = spec.Id,
            ProjectId = spec.ProjectId,
            ProjectName = spec.Project?.Name ?? string.Empty,
            Title = spec.Title,
            Description = spec.Description,
            Status = spec.Status,
            CreatedAt = spec.CreatedAt,
            CurrentVersionNumber = currentVersionNumber,
            CurrentAcceptanceCriteria = currentVersion?.AcceptanceCriteria.Select(ac => ac.Text).ToList() ?? new List<string>(),
            CurrentScopeTags = currentVersion?.ScopeTags.Select(st => st.TagName).ToList() ?? new List<string>(),
            Versions = publishedVersions
                .Select(v => new SpecVersionDto
                {
                    Id = v.Id,
                    SpecId = v.SpecId,
                    VersionNumber = v.VersionNumber,
                    Content = v.Content,
                    PublishedAt = v.PublishedAt,
                    AcceptanceCriteria = v.AcceptanceCriteria.Select(ac => ac.Text).ToList(),
                    ScopeTags = v.ScopeTags.Select(st => st.TagName).ToList()
                })
                .ToList()
        };
    }
}
