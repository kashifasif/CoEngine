using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpecPlatform.Api.Data;
using SpecPlatform.Api.Services;
using SpecPlatform.Shared.DTOs;
using SpecPlatform.Shared.Models;
using Microsoft.SemanticKernel;

namespace SpecPlatform.Api.Controllers;

[Authorize]
[ApiController]
public class SpecsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOpenRouterService _openRouter;
    private readonly IVectorStoreService _vectorStore;
    private readonly ILogger<SpecsController> _logger;

    private readonly ICurrentUserService _currentUser;

    public SpecsController(AppDbContext db, IOpenRouterService openRouter, IVectorStoreService vectorStore,
        ILogger<SpecsController> logger, ICurrentUserService currentUser)
    {
        _db = db;
        _openRouter = openRouter;
        _vectorStore = vectorStore;
        _logger = logger;
        _currentUser = currentUser;
    }

    private async Task RecordAiUsageAsync(string operation, string promptText, string completionText,
        string modelName = "openrouter/anthropic/claude-3.5-sonnet")
    {
        try
        {
            var user = await _currentUser.GetUserAsync();
            var userId = user?.Id;
            var username = user?.Username ?? "anonymous";

            int promptTokens = Math.Max(1, (promptText?.Length ?? 0) / 4);
            int completionTokens = Math.Max(1, (completionText?.Length ?? 0) / 4);
            int totalTokens = promptTokens + completionTokens;

            decimal estCost = ((promptTokens * 0.000003m) + (completionTokens * 0.000015m));

            var usage = new UserAiUsage
            {
                UserId = userId,
                Username = username,
                Operation = operation,
                ModelName = modelName,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                EstimatedCostUsd = estCost,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserAiUsages.Add(usage);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record AI token usage.");
        }
    }

    [HttpGet("api/users/me/ai-usage")]
    public async Task<ActionResult<UserAiUsageSummaryDto>> GetCurrentUserAiUsage()
    {
        var user = await _currentUser.GetUserAsync();
        var userId = user?.Id;
        var username = user?.Username;

        var query = _db.UserAiUsages.AsQueryable();
        if (userId.HasValue)
        {
            query = query.Where(u => u.UserId == userId.Value || (username != null && u.Username == username));
        }

        var list = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

        int totalTokens = list.Sum(u => u.TotalTokens);
        int promptTokens = list.Sum(u => u.PromptTokens);
        int completionTokens = list.Sum(u => u.CompletionTokens);
        decimal totalCost = list.Sum(u => u.EstimatedCostUsd);

        var breakdown = list
            .GroupBy(u => u.Operation)
            .Select(g => new AiUsageByOperationDto
            {
                Operation = g.Key,
                TotalTokens = g.Sum(x => x.TotalTokens),
                RequestsCount = g.Count(),
                Percentage = totalTokens > 0 ? Math.Round((decimal)g.Sum(x => x.TotalTokens) / totalTokens * 100, 1) : 0
            })
            .OrderByDescending(b => b.TotalTokens)
            .ToList();

        var recentLogs = list
            .Take(20)
            .Select(u => new UserAiUsageRecordDto
            {
                Id = u.Id,
                Operation = u.Operation,
                ModelName = u.ModelName,
                PromptTokens = u.PromptTokens,
                CompletionTokens = u.CompletionTokens,
                TotalTokens = u.TotalTokens,
                EstimatedCostUsd = u.EstimatedCostUsd,
                CreatedAt = u.CreatedAt
            })
            .ToList();

        return Ok(new UserAiUsageSummaryDto
        {
            TotalTokens = totalTokens,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalRequests = list.Count,
            EstimatedCostUsd = totalCost,
            BreakdownByOperation = breakdown,
            RecentLogs = recentLogs
        });
    }

    [HttpGet("api/projects/{projectId:guid}/specs")]
    public async Task<ActionResult<List<SpecDto>>> GetSpecsForProject(Guid projectId)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
        if (!projectExists)
        {
            return NotFound(new { message = $"Project {projectId} not found." });
        }

        var specs = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AuthorUser)
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(specs.Select(MapToSpecDto).ToList());
    }

    [HttpDelete("api/projects/{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == currentUser.Id);
        if (project == null) return NotFound(new { message = $"Project {id} not found." });

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();

        await _vectorStore.ClearProjectVectorsAsync(id);

        return Ok(new { success = true, message = $"Project {id} deleted successfully." });
    }

    [HttpDelete("api/specs/{id:guid}")]
    public async Task<IActionResult> DeleteSpec(Guid id)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var spec = await _db.Specs.Include(s => s.Project).FirstOrDefaultAsync(s => s.Id == id && s.Project.CreatedByUserId == currentUser.Id);
        if (spec == null) return NotFound(new { message = $"Spec {id} not found." });

        _db.Specs.Remove(spec);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = $"Spec {id} deleted successfully." });
    }

    [HttpPost("api/projects/{projectId:guid}/specs")]
    public async Task<ActionResult<SpecDto>> CreateSpec(Guid projectId, [FromBody] CreateSpecDto dto)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
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
            CreatedByUserId = currentUser?.Id,
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
            AuthorUserId = currentUser?.Id,
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

        // Index Spec into Vector Store (instantly searchable for Dev/QA & BA)
        var criteriaText = string.Join(". ", dto.AcceptanceCriteria);
        var tagsText = string.Join(", ", dto.ScopeTags);
        await _vectorStore.IndexDocumentAsync(projectId, "spec", spec.Title,
            $"{spec.Description}. Acceptance criteria: {criteriaText}. Scope tags: {tagsText}");

        spec.Project = project;
        spec.CreatedByUser = currentUser;
        return CreatedAtAction(nameof(GetSpec), new { id = spec.Id }, MapToSpecDto(spec));
    }

    [HttpPost("api/projects/{projectId:guid}/publish-master-spec")]
    public async Task<ActionResult<SpecDto>> PublishMasterSpecForProject(Guid projectId, [FromBody] CreateSpecDto dto)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
        if (project == null)
        {
            return NotFound(new { message = $"Project {projectId} not found." });
        }

        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId);

        if (spec == null)
        {
            spec = new Spec
            {
                ProjectId = projectId,
                Title = string.IsNullOrWhiteSpace(dto.Title) ? $"{project.Name} Specification" : dto.Title.Trim(),
                Description = dto.Description?.Trim() ?? string.Empty,
                Status = "Published",
                CreatedByUserId = currentUser?.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.Specs.Add(spec);
            await _db.SaveChangesAsync();

            var v1 = new SpecVersion
            {
                SpecId = spec.Id,
                VersionNumber = 1,
                Content = spec.Description,
                PublishedAt = DateTime.UtcNow,
                AuthorUserId = currentUser?.Id,
                AcceptanceCriteria = dto.AcceptanceCriteria
                    .Where(ac => !string.IsNullOrWhiteSpace(ac))
                    .Select(ac => new AcceptanceCriterion { Text = ac.Trim() })
                    .ToList(),
                ScopeTags = dto.ScopeTags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => new ScopeTag { TagName = t.Trim() })
                    .ToList(),
                SelfReviewJson = dto.SelfReviewJson
            };
            _db.SpecVersions.Add(v1);
            await _db.SaveChangesAsync();

            var criteriaText = string.Join(". ", dto.AcceptanceCriteria);
            var tagsText = string.Join(", ", dto.ScopeTags);
            await _vectorStore.IndexDocumentAsync(projectId, "spec", spec.Title,
                $"{spec.Description}. Acceptance criteria: {criteriaText}. Scope tags: {tagsText}");

            spec.Project = project;
            return Ok(MapToSpecDto(spec));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(dto.Title))
            {
                spec.Title = dto.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.Description))
            {
                spec.Description = dto.Description.Trim();
            }

            spec.Status = "Published";

            var publishedVersions = spec.Versions.Where(v => v.VersionNumber > 0)
                .OrderByDescending(v => v.VersionNumber).ToList();
            int nextVersionNumber = publishedVersions.Any() ? publishedVersions.First().VersionNumber + 1 : 1;

            var newVersion = new SpecVersion
            {
                SpecId = spec.Id,
                VersionNumber = nextVersionNumber,
                Content = dto.Description,
                PublishedAt = DateTime.UtcNow,
                AuthorUserId = currentUser?.Id,
                AcceptanceCriteria = dto.AcceptanceCriteria
                    .Where(ac => !string.IsNullOrWhiteSpace(ac))
                    .Select(ac => new AcceptanceCriterion { Text = ac.Trim() })
                    .ToList(),
                ScopeTags = dto.ScopeTags
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => new ScopeTag { TagName = t.Trim() })
                    .ToList(),
                SelfReviewJson = dto.SelfReviewJson
            };

            _db.SpecVersions.Add(newVersion);
            await _db.SaveChangesAsync();

            var criteriaText = string.Join(". ", dto.AcceptanceCriteria);
            var tagsText = string.Join(", ", dto.ScopeTags);
            await _vectorStore.IndexDocumentAsync(projectId, "spec", spec.Title,
                $"{spec.Description}. Acceptance criteria: {criteriaText}. Scope tags: {tagsText}");

            var notification = new Notification
            {
                SpecId = spec.Id,
                SpecTitle = spec.Title,
                ProjectName = project.Name,
                VersionNumber = nextVersionNumber,
                SummaryText =
                    $"[NOTIFICATION] Master Specification for project '{project.Name}' published as Version {nextVersionNumber}.",
                AuthorUserId = currentUser?.Id,
                ActionType = "published",
                CreatedAt = DateTime.UtcNow
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            return Ok(MapToSpecDto(spec));
        }
    }

    [HttpGet("api/specs/{id:guid}")]
    public async Task<ActionResult<SpecDto>> GetSpec(Guid id)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AuthorUser)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project.CreatedByUserId == currentUser.Id);

        if (spec == null)
        {
            return NotFound(new { message = $"Spec {id} not found." });
        }

        return Ok(MapToSpecDto(spec));
    }

    [HttpPut("api/specs/{id:guid}")]
    public async Task<ActionResult<SpecDto>> UpdateDraftSpec(Guid id, [FromBody] UpdateSpecDto dto)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project.CreatedByUserId == currentUser.Id);

        if (spec == null)
        {
            return NotFound(new { message = $"Spec {id} not found." });
        }

        _db.Entry(spec).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;

        spec.Title = dto.Title.Trim();
        spec.Description = dto.Description?.Trim() ?? string.Empty;

        var draftVersion = spec.Versions.FirstOrDefault(v => v.VersionNumber == 0);
        if (draftVersion == null)
        {
            var latestPublished = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
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

        spec.RowVersion = Guid.NewGuid();

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "This spec was updated by someone else since you last loaded it. Please refresh and reapply your changes." });
        }

        // Index Spec Update into Vector Store (instantly searchable for Dev/QA & BA)
        var updatedCriteriaText = string.Join(". ", dto.AcceptanceCriteria);
        var updatedTagsText = string.Join(", ", dto.ScopeTags);
        await _vectorStore.IndexDocumentAsync(spec.ProjectId, "spec", spec.Title,
            $"{spec.Description}. Acceptance criteria: {updatedCriteriaText}. Scope tags: {updatedTagsText}");

        return Ok(MapToSpecDto(spec));
    }

    [HttpPost("api/specs/{id:guid}/publish")]
    public async Task<ActionResult<PublishResultDto>> PublishSpec(Guid id)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project.CreatedByUserId == currentUser.Id);

        if (spec == null)
        {
            return NotFound(new { message = $"Spec {id} not found." });
        }

        var publishedVersions = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber)
            .ToList();
        int nextVersionNumber = publishedVersions.Any() ? publishedVersions.First().VersionNumber + 1 : 1;

        var draftVersion = spec.Versions.FirstOrDefault(v => v.VersionNumber == 0)
                           ?? spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        var newPublishedVersion = new SpecVersion
        {
            SpecId = spec.Id,
            VersionNumber = nextVersionNumber,
            Content = spec.Description,
            PublishedAt = DateTime.UtcNow,
            AuthorUserId = currentUser?.Id,
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
        var summaryText =
            $"[NOTIFICATION] Spec '{spec.Title}' in project '{spec.Project?.Name}' published as Version {nextVersionNumber}. Contains {criteriaCount} acceptance criteria and {tagsCount} scope tags.";

        var notification = new Notification
        {
            SpecId = spec.Id,
            SpecTitle = spec.Title,
            ProjectName = spec.Project?.Name ?? "General",
            VersionNumber = nextVersionNumber,
            SummaryText = summaryText,
            AuthorUserId = currentUser?.Id,
            ActionType = "published",
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        // Index Published Version into Vector Store (ONLY Published Specs are indexed for Dev/QA)
        var pubCriteria = string.Join(". ", newPublishedVersion.AcceptanceCriteria.Select(a => a.Text));
        var pubTags = string.Join(", ", newPublishedVersion.ScopeTags.Select(t => t.TagName));
        await _vectorStore.IndexDocumentAsync(spec.ProjectId, "published_spec", $"{spec.Title} (v{nextVersionNumber})",
            $"{spec.Description}. Acceptance criteria: {pubCriteria}. Scope tags: {pubTags}");

        _logger.LogInformation(summaryText);

        return Ok(new PublishResultDto
        {
            Spec = MapToSpecDto(spec),
            NotificationSummary = summaryText
        });
    }

    [HttpPost("api/specs/{id:guid}/versions/{versionNumber:int}/undo")]
    public async Task<ActionResult<SpecDto>> UndoPublishSpec(Guid id, int versionNumber)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project.CreatedByUserId == currentUser.Id);

        if (spec == null) return NotFound("Spec not found.");

        var targetVersion = spec.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
        if (targetVersion == null || targetVersion.VersionNumber == 0) return BadRequest("Invalid version number.");
        if (targetVersion.IsUndone) return BadRequest("Version is already undone.");

        targetVersion.IsUndone = true;

        var summaryText = $"Undone publication of Specification: {spec.Title} (v{versionNumber})";
        var notification = new Notification
        {
            SpecId = spec.Id,
            SpecTitle = spec.Title,
            ProjectName = spec.Project?.Name ?? "General",
            VersionNumber = versionNumber,
            SummaryText = summaryText,
            AuthorUserId = currentUser?.Id,
            ActionType = "undone",
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);

        await _db.SaveChangesAsync();
        await _vectorStore.DeleteDocumentAsync(spec.ProjectId, $"published_spec", $"{spec.Title} (v{versionNumber})");

        return Ok(MapToSpecDto(spec));
    }

    [HttpPost("api/specs/{id:guid}/versions/{versionNumber:int}/redo")]
    public async Task<ActionResult<SpecDto>> RedoPublishSpec(Guid id, int versionNumber)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var spec = await _db.Specs
            .Include(s => s.Project)
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.Id == id && s.Project.CreatedByUserId == currentUser.Id);

        if (spec == null) return NotFound("Spec not found.");

        var targetVersion = spec.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
        if (targetVersion == null || targetVersion.VersionNumber == 0) return BadRequest("Invalid version number.");
        if (!targetVersion.IsUndone) return BadRequest("Version is not undone.");

        targetVersion.IsUndone = false;

        var summaryText = $"Redone publication of Specification: {spec.Title} (v{versionNumber})";
        var notification = new Notification
        {
            SpecId = spec.Id,
            SpecTitle = spec.Title,
            ProjectName = spec.Project?.Name ?? "General",
            VersionNumber = versionNumber,
            SummaryText = summaryText,
            AuthorUserId = currentUser?.Id,
            ActionType = "restored",
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);

        await _db.SaveChangesAsync();

        var pubCriteria = string.Join(". ", targetVersion.AcceptanceCriteria.Select(a => a.Text));
        var pubTags = string.Join(", ", targetVersion.ScopeTags.Select(t => t.TagName));
        await _vectorStore.IndexDocumentAsync(spec.ProjectId, "published_spec", $"{spec.Title} (v{versionNumber})",
            $"{spec.Description}. Acceptance criteria: {pubCriteria}. Scope tags: {pubTags}");

        return Ok(MapToSpecDto(spec));
    }

    [HttpGet("api/notifications")]
    public async Task<ActionResult<List<NotificationDto>>> GetNotifications([FromQuery] int skip = 0,
        [FromQuery] int take = 10)
    {
        var notifications = await _db.Notifications
            .Include(n => n.AuthorUser)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                SpecId = n.SpecId,
                SpecTitle = n.SpecTitle,
                ProjectName = n.ProjectName,
                VersionNumber = n.VersionNumber,
                SummaryText = n.SummaryText,
                CreatedAt = n.CreatedAt,
                AuthorDisplayName = n.AuthorUser != null ? n.AuthorUser.DisplayName : null,
                AuthorUsername = n.AuthorUser != null ? n.AuthorUser.Username : null,
                AuthorAvatarUrl = n.AuthorUser != null ? n.AuthorUser.AvatarUrl : null,
                ActionType = n.ActionType ?? "published"
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("api/specs/{id:guid}/versions/{v1:int}/diff/{v2:int}")]
    public async Task<ActionResult<SpecDiffDto>> CompareVersions(Guid id, int v1, int v2)
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

        var summary =
            $"Version {v1} -> {v2}: {addedCriteria.Count} criteria added, {removedCriteria.Count} criteria removed, {addedTags.Count} tags added, {removedTags.Count} tags removed.";

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

    [HttpGet("api/projects/{projectId:guid}/vector-store")]
    public async Task<ActionResult<VectorStoreStatsDto>> GetVectorStoreStats(Guid projectId)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
        if (!projectExists) return NotFound(new { message = "Project not found or access denied." });

        var stats = await _vectorStore.GetStatsAsync(projectId);
        return Ok(stats);
    }

    [HttpPost("api/specs/draft/chat")]
    public async Task<ActionResult<ChatResponseDto>> BrainstormChat([FromBody] ChatRequestDto request)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.CreatedByUserId == currentUser.Id);
        if (project == null) return NotFound(new { message = "Project not found or access denied." });

        var projectName = project.Name;
        var projectDesc = project.Description;

        var userQuery = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";

        var allVectorMatches = await _vectorStore.SearchSimilarityAsync(request.ProjectId, userQuery, topK: 3);
        var vectorMatches = allVectorMatches
            .Where(m => m.DocType == "spec" || m.DocType == "published_spec" || m.DocType == "draft_spec").ToList();

        var specContext = new StringBuilder();
        if (vectorMatches.Any())
        {
            specContext.AppendLine($"\n--- SEMANTIC RELEVANCE VECTOR MATCHES (EXISTING SPECS) ---");
            foreach (var match in vectorMatches)
            {
                specContext.AppendLine($"[Match: {match.Title}] {match.Content}");
            }
        }

        string systemPrompt;
        if (!request.IsClarificationPhase)
        {
            systemPrompt =
                $"STRICT PROJECT ISOLATION BOUNDARY: You are strictly scoped ONLY to Project: '{{{{$projectName}}}}' ({{{{$projectDesc}}}}). You must NEVER reference, mix, or assume requirements/knowledge from any other project.\n\n" +
                "You are a Requirements Brainstorming Assistant, currently in LISTENING MODE.\n\n" +
                $"CONTEXT: You are helping a Product Owner (PO) or Business Analyst (BA) brainstorm a feature for the project: {{{{$projectName}}}} — {{{{$projectDesc}}}}\n\n" +
                "{{$specContext}}\n\n" +
                "Your job right now is to let the PO/BA freely describe a feature idea, without interrupting with clarifying questions about the feature.\n\n" +
                "STRICT RULES:\n" +
                "1. Do NOT ask any clarifying questions in this phase to build the spec. Let the user talk.\n" +
                "2. If the user is simply providing information or brainstorming, respond only with brief, natural acknowledgments.\n" +
                "3. IF the user asks you a direct question, you MUST answer their question directly, helpfully, and concisely based on your knowledge and the existing spec context.\n" +
                "4. Do NOT summarize or critique what they've said, UNLESS they explicitly ask for your opinion.\n" +
                "5. Keep every response short and focused. You are listening and assisting, not leading the interrogation.\n" +
                "Wait for the PO/BA to explicitly signal they are done before any clarification rounds happen.";
        }
        else
        {
            systemPrompt =
                $"STRICT PROJECT ISOLATION BOUNDARY: You are strictly scoped ONLY to Project: '{{{{$projectName}}}}' ({{{{$projectDesc}}}}). You must NEVER reference, mix, or assume requirements/knowledge from any other project.\n\n" +
                "You are a Requirements Clarification Assistant. Your ONLY job is to help a Product Owner (PO) or Business Analyst (BA) think through a feature idea by identifying what is unclear or missing, and asking clarifying questions.\n\n" +
                $"CONTEXT: Project: {{{{$projectName}}}} — {{{{$projectDesc}}}}\n\n" +
                "{{$specContext}}\n\n" +
                "You will be given:\n" +
                "1. EXISTING SPECIFICATION & UNRESOLVED OPEN BUSINESS QUESTIONS (via context above).\n" +
                "2. The full brainstorming conversation so far.\n" +
                "STRICT RULES:\n" +
                "1. Your response must ALWAYS be a numbered list of clarifying questions. For EACH question, provide 2 to 4 suggested options (A, B, C...) to make it easy for the PO/BA to answer.\n" +
                "2. Ask a MAXIMUM of 5 questions per round. Never more.\n" +
                "3. PRIORITIZE UNRESOLVED OPEN BUSINESS QUESTIONS: If the existing spec or previous rounds have unresolved Open Business Questions, YOU MUST TURN THOSE INTO CLARIFYING QUESTIONS.\n" +
                "4. SCOPE RESTRICTION - FUNCTIONAL REQUIREMENTS ONLY: Questions must stay strictly focused on functional/business requirements (what the system should do, for whom, under what conditions). Do NOT ask technical or implementation-level questions (e.g., \"how will the API authenticate this request?\", database design, architecture choices) — the people answering are business stakeholders, not developers.\n" +
                "5. Only ask questions that are genuinely unclear, ambiguous, missing, or would cause a developer to guess.\n" +
                "6. Do NOT ask about anything already clearly answered and established in the existing spec.\n" +
                "OUTPUT FORMAT (strict):\n" +
                "1. [Question text]\n" +
                "   - A) [Option 1]\n" +
                "   - B) [Option 2]\n";
        }

        var args = new KernelArguments
        {
            { "projectName", projectName },
            { "projectDesc", projectDesc },
            { "specContext", specContext.ToString() }
        };

        var response = await _openRouter.ChatAsync(systemPrompt, args, request.Messages);
        if (response.Success)
        {
            await RecordAiUsageAsync("PO Brainstorming Chat",
                systemPrompt + string.Join(" ", request.Messages.Select(m => m.Content)), response.Reply);
        }

        return Ok(response);
    }

    // --- DB Chat Session Persistence & Vector Store Endpoints ---

    [HttpGet("api/projects/{projectId:guid}/chat-session/{personaMode}")]
    public async Task<ActionResult<ChatSessionDto>> GetOrCreateChatSession(
        Guid projectId,
        string personaMode,
        [FromQuery] int? skip = null,
        [FromQuery] int? take = null)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
        if (!projectExists) return NotFound(new { message = "Project not found or access denied." });

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

        var allSorted = session.Messages.OrderBy(m => m.Timestamp).ToList();
        var totalCount = allSorted.Count;

        List<ChatMessageRecord> resultMessages = allSorted;
        bool hasMore = false;

        if (take.HasValue && take.Value > 0)
        {
            int skipCount = skip ?? 0;
            int takeCount = take.Value;

            int actualSkip = Math.Max(0, totalCount - skipCount - takeCount);
            int actualTake = Math.Min(takeCount, Math.Max(0, totalCount - skipCount));

            if (actualTake > 0 && actualSkip < totalCount)
            {
                resultMessages = allSorted.Skip(actualSkip).Take(actualTake).ToList();
                hasMore = actualSkip > 0;
            }
            else
            {
                resultMessages = new List<ChatMessageRecord>();
                hasMore = false;
            }
        }

        return Ok(new ChatSessionDto
        {
            Id = session.Id,
            ProjectId = session.ProjectId,
            PersonaMode = session.PersonaMode,
            UpdatedAt = session.UpdatedAt,
            TotalMessagesCount = totalCount,
            HasMore = hasMore,
            ClarificationRoundNumber = session.ClarificationRoundNumber,
            Messages = resultMessages.Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content,
                AttachedFileName = m.AttachedFileName,
                AttachedFileUrl = m.AttachedFileUrl
            }).ToList()
        });
    }

    [HttpPost("api/projects/{projectId:guid}/chat-session/{personaMode}/messages")]
    public async Task<IActionResult> SaveChatMessages(Guid projectId, string personaMode,
        [FromBody] List<ChatMessageDto> newMessages)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
        if (!projectExists) return NotFound(new { message = "Project not found or access denied." });

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
                AttachedFileName = msg.AttachedFileName,
                AttachedFileUrl = msg.AttachedFileUrl,
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

    [HttpDelete("api/projects/{projectId:guid}/chat-session/{personaMode}")]
    public async Task<IActionResult> ClearChatSession(Guid projectId, string personaMode)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId && p.CreatedByUserId == currentUser.Id);
        if (!projectExists) return NotFound(new { message = "Project not found or access denied." });

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

        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null)
        {
            Response.StatusCode = 401;
            return;
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.CreatedByUserId == currentUser.Id, cancellationToken);
        if (project == null)
        {
            Response.StatusCode = 404;
            return;
        }

        if (request.IsClarificationPhase)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(cs => cs.ProjectId == request.ProjectId && cs.PersonaMode == "po_brainstorming", cancellationToken);
            if (session != null)
            {
                if (session.ClarificationRoundNumber >= 3)
                {
                    Response.StatusCode = 400;
                    await Response.WriteAsync("[API Error] Maximum clarification rounds (3) reached.");
                    return;
                }
                
                session.ClarificationRoundNumber++;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var projectName = project.Name;
        var projectDesc = project.Description;

        var userQuery = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";

        var allVectorMatches = await _vectorStore.SearchSimilarityAsync(request.ProjectId, userQuery, topK: 3);
        var vectorMatches = allVectorMatches
            .Where(m => m.DocType == "spec" || m.DocType == "published_spec" || m.DocType == "draft_spec").ToList();

        var specContext = new StringBuilder();
        if (vectorMatches.Any())
        {
            specContext.AppendLine($"\n--- SEMANTIC RELEVANCE VECTOR MATCHES (EXISTING SPECS) ---");
            foreach (var match in vectorMatches)
            {
                specContext.AppendLine($"[Match: {match.Title}] {match.Content}");
            }
        }

        string systemPrompt;
        if (!request.IsClarificationPhase)
        {
            systemPrompt =
                $"STRICT PROJECT ISOLATION BOUNDARY: You are strictly scoped ONLY to Project: '{{{{$projectName}}}}' ({{{{$projectDesc}}}}). You must NEVER reference, mix, or assume requirements/knowledge from any other project.\n\n" +
                "You are a Requirements Brainstorming Assistant, currently in LISTENING MODE.\n\n" +
                $"CONTEXT: You are helping a Product Owner (PO) or Business Analyst (BA) brainstorm a feature for the project: {{{{$projectName}}}} — {{{{$projectDesc}}}}\n\n" +
                "{{$specContext}}\n\n" +
                "Your job right now is to let the PO/BA freely describe a feature idea, without interrupting with clarifying questions about the feature.\n\n" +
                "STRICT RULES:\n" +
                "1. Do NOT ask any clarifying questions in this phase to build the spec. Let the user talk.\n" +
                "2. If the user is simply providing information or brainstorming, respond only with brief, natural acknowledgments.\n" +
                "3. IF the user asks you a direct question, you MUST answer their question directly, helpfully, and concisely based on your knowledge and the existing spec context.\n" +
                "4. Do NOT summarize or critique what they've said, UNLESS they explicitly ask for your opinion.\n" +
                "5. Keep every response short and focused. You are listening and assisting, not leading the interrogation.\n" +
                "Wait for the PO/BA to explicitly signal they are done before any clarification rounds happen.";
        }
        else
        {
            systemPrompt =
                $"STRICT PROJECT ISOLATION BOUNDARY: You are strictly scoped ONLY to Project: '{{{{$projectName}}}}' ({{{{$projectDesc}}}}). You must NEVER reference, mix, or assume requirements/knowledge from any other project.\n\n" +
                "You are a Requirements Clarification Assistant. Your ONLY job is to help a Product Owner (PO) or Business Analyst (BA) think through a feature idea by identifying what is unclear or missing, and asking clarifying questions.\n\n" +
                $"CONTEXT: Project: {{{{$projectName}}}} — {{{{$projectDesc}}}}\n\n" +
                "{{$specContext}}\n\n" +
                "You will be given:\n" +
                "1. EXISTING SPECIFICATION & UNRESOLVED OPEN BUSINESS QUESTIONS (via context above).\n" +
                "2. The full brainstorming conversation so far.\n" +
                "STRICT RULES:\n" +
                "1. Your response must ALWAYS be a numbered list of clarifying questions. For EACH question, provide 2 to 4 suggested options (A, B, C...) to make it easy for the PO/BA to answer.\n" +
                "2. Ask a MAXIMUM of 5 questions per round. Never more.\n" +
                "3. PRIORITIZE UNRESOLVED OPEN BUSINESS QUESTIONS: If the existing spec or previous rounds have unresolved Open Business Questions, YOU MUST TURN THOSE INTO CLARIFYING QUESTIONS.\n" +
                "4. SCOPE RESTRICTION - FUNCTIONAL REQUIREMENTS ONLY: Questions must stay strictly focused on functional/business requirements (what the system should do, for whom, under what conditions). Do NOT ask technical or implementation-level questions (e.g., \"how will the API authenticate this request?\", database design, architecture choices) — the people answering are business stakeholders, not developers.\n" +
                "5. Only ask questions that are genuinely unclear, ambiguous, missing, or would cause a developer to guess.\n" +
                "6. CRITICAL: CAREFULLY REVIEW THE ENTIRE CHAT HISTORY before asking a question. You MUST NOT ask any question that has already been asked and answered in a previous round.\n" +
                "7. Do NOT ask about anything already clearly answered and established in the existing spec or the chat history.\n" +
                "OUTPUT FORMAT (strict):\n" +
                "1. [Question text]\n" +
                "   - A) [Option 1]\n" +
                "   - B) [Option 2]\n";
        }

        var args = new KernelArguments
        {
            { "projectName", projectName },
            { "projectDesc", projectDesc },
            { "specContext", specContext.ToString() }
        };

        var streamAccumulator = new StringBuilder();
        await foreach (var chunk in
                       _openRouter.ChatStreamAsync(systemPrompt, args, request.Messages, cancellationToken))
        {
            streamAccumulator.Append(chunk);
            await Response.WriteAsync(chunk, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        await RecordAiUsageAsync(request.IsClarificationPhase ? "PO Clarification Questions" : "PO Brainstorming",
            systemPrompt + string.Join(" ", request.Messages.Select(m => m.Content)),
            streamAccumulator.ToString());
    }

    [HttpPost("api/specs/query/chat/stream")]
    public async Task DevQaQueryStream(
        [FromBody] DevQaQueryRequestDto request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";

        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null)
        {
            Response.StatusCode = 401;
            return;
        }

        var project = await _db.Projects
            .Include(p => p.Specs)
            .ThenInclude(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(p => p.Specs)
            .ThenInclude(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.CreatedByUserId == currentUser.Id, cancellationToken);
        
        if (project == null)
        {
            Response.StatusCode = 404;
            return;
        }

        var projectName = project.Name;
        var projectDesc = project.Description;

        var userQuery = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";

        // Query Vector Store for top semantically relevant specs!
        var allVectorMatches = await _vectorStore.SearchSimilarityAsync(request.ProjectId, userQuery, topK: 5);
        var vectorMatches = allVectorMatches
            .Where(m => m.DocType == "spec" || m.DocType == "published_spec" || m.DocType == "draft_spec").ToList();

        var specContext = new StringBuilder();
        specContext.AppendLine($"--- ALL LATEST PROJECT SPECIFICATIONS (FULL LATEST REQUIREMENTS) ---");

        if (project?.Specs != null && project.Specs.Any())
        {
            foreach (var spec in project.Specs)
            {
                var latestVer = spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

                specContext.AppendLine(
                    $"\n[Spec #{spec.Id} | Title: {spec.Title} | Status: {spec.Status} | Latest Version: v{latestVer?.VersionNumber ?? 0}]");
                specContext.AppendLine($"Description: {spec.Description}");

                if (latestVer?.AcceptanceCriteria.Any() == true)
                {
                    specContext.AppendLine("Acceptance Criteria:");
                    foreach (var ac in latestVer.AcceptanceCriteria)
                    {
                        specContext.AppendLine($" - {ac.Text}");
                    }
                }

                if (latestVer?.ScopeTags.Any() == true)
                {
                    specContext.AppendLine("Scope Tags: " +
                                           string.Join(", ", latestVer.ScopeTags.Select(t => t.TagName)));
                }
            }
        }
        else
        {
            specContext.AppendLine("\n[Notice: No specifications created for this project yet.]");
        }

        if (vectorMatches.Any())
        {
            specContext.AppendLine($"\n--- SEMANTIC RELEVANCE VECTOR MATCHES ---");
            foreach (var match in vectorMatches)
            {
                specContext.AppendLine(
                    $"[Vector Match: {match.Title} | Similarity: {match.SimilarityScore:F2}] {match.Content}");
            }
        }

        string roleInstructions =
            $"You are an expert AI Technical Specification Q&A Assistant for Project: '{{{{$projectName}}}}'. Help Developers, QA Engineers, Product Managers, and Team Members understand technical implementation details, microservice boundaries, API payloads, DB schema impacts, test scenarios, edge cases, and business logic BASED ON THE LATEST PROJECT SPECIFICATIONS ABOVE.";

        var systemPrompt =
            $"STRICT PROJECT ISOLATION BOUNDARY: You are strictly scoped ONLY to Project: '{{{{$projectName}}}}' ({{{{$projectDesc}}}}).\n" +
            "MANDATE: Answer the user's question accurately using the project specifications provided above. Do NOT mix, reference, or assume data from any other project.\n\n" +
            $"{roleInstructions}\n\nProject Overview: {{{{$projectDesc}}}}\n\n{{{{$specContext}}}}\n\n" +
            "STRICT QA RULES:\n" +
            "1. Answers MUST be short and to the point. Directly quote or closely paraphrase the relevant part of the published spec. No elaboration, no added opinions, no information not explicitly present in the spec.\n" +
            "2. If the answer isn't found in the published spec, say so plainly: \"Not specified in the published spec\" rather than inferring or guessing an answer.\n" +
            "3. Do NOT ask follow-up questions back to the dev/QA user.\n\n" +
            "Goal: Answer the query accurately based on the Specifications above.";

        var args = new KernelArguments
        {
            { "projectName", projectName },
            { "projectDesc", projectDesc },
            { "specContext", specContext.ToString() }
        };

        var streamAccumulator = new StringBuilder();
        await foreach (var chunk in
                       _openRouter.ChatStreamAsync(systemPrompt, args, request.Messages, cancellationToken))
        {
            streamAccumulator.Append(chunk);
            await Response.WriteAsync(chunk, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        await RecordAiUsageAsync("Dev & QA Assistant",
            systemPrompt + string.Join(" ", request.Messages.Select(m => m.Content)),
            streamAccumulator.ToString());
    }

    private async Task<string> GetExistingSpecContextAsync(Guid projectId)
    {
        var existingSpec = await _db.Specs
            .Include(s => s.Versions)
            .ThenInclude(v => v.AcceptanceCriteria)
            .Include(s => s.Versions)
            .ThenInclude(v => v.ScopeTags)
            .FirstOrDefaultAsync(s => s.ProjectId == projectId);

        if (existingSpec == null) return string.Empty;

        var publishedVersions = existingSpec.Versions
            .Where(v => v.VersionNumber > 0)
            .OrderBy(v => v.VersionNumber)
            .ToList();

        if (!publishedVersions.Any()) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("=== ALL PREVIOUS PUBLISHED SPECIFICATION VERSIONS (FROM v1 TO LATEST) ===");
        sb.AppendLine($"Master Specification Title: {existingSpec.Title}");

        foreach (var ver in publishedVersions)
        {
            var allAc = ver.AcceptanceCriteria.Select(a => a.Text).ToList();
            var openQuestions = allAc.Where(a => a.StartsWith("❓"))
                .Select(a => a.Replace("❓ **Open Question:**", "").Replace("❓ **Open Business Question:**", "").Trim())
                .ToList();
            var normalAc = allAc.Where(a => !a.StartsWith("❓")).ToList();

            var acText = normalAc.Any() ? string.Join("\n  - ", normalAc) : "None";
            var oqText = openQuestions.Any() ? string.Join("\n  - ", openQuestions) : "None";
            var tagsText = ver.ScopeTags.Any()
                ? string.Join(", ", ver.ScopeTags.Select(t => t.TagName))
                : "bff, api, mfe";

            sb.AppendLine($"\n--- Published Version v{ver.VersionNumber} (Published: {ver.PublishedAt:g}) ---");
            sb.AppendLine($"Description / SRS Narrative:\n{ver.Content}");
            sb.AppendLine($"Acceptance Criteria & Requirements:\n  - {acText}");
            sb.AppendLine($"Scope Tags: [{tagsText}]");
            sb.AppendLine($"Unresolved Open Questions:\n  - {oqText}");
        }

        sb.AppendLine("\n========================================================================\n");
        return sb.ToString();
    }

    [HttpPost("api/specs/draft/structure")]
    public async Task<ActionResult<StructuredSpecResultDto>> StructureChat([FromBody] ChatRequestDto request)
    {
        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.CreatedByUserId == currentUser.Id);
        if (project == null) return NotFound(new { message = "Project not found or access denied." });

        var projectName = project.Name;
        var projectDesc = project.Description;

        var existingSpecContext = await GetExistingSpecContextAsync(request.ProjectId);

        // Load complete DB Chat Session messages to guarantee 100% transcript coverage from start!
        var dbSession = await _db.ChatSessions
            .Include(cs => cs.Messages)
            .FirstOrDefaultAsync(cs => cs.ProjectId == request.ProjectId && cs.PersonaMode == "po_brainstorming");

        var messagesToUse = new List<ChatMessageDto>();

        if (dbSession != null && dbSession.Messages.Any())
        {
            messagesToUse.AddRange(dbSession.Messages.OrderBy(m => m.Timestamp).Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content
            }));
        }

        if (request.Messages != null && request.Messages.Any())
        {
            foreach (var reqMsg in request.Messages)
            {
                if (!messagesToUse.Any(m => m.Role == reqMsg.Role && m.Content == reqMsg.Content))
                {
                    messagesToUse.Add(reqMsg);
                }
            }
        }

        if (!messagesToUse.Any())
        {
            return BadRequest(new { message = "You have not provided any brainstorming data or draft information. Please start a conversation first." });
        }

        var systemPrompt = $"CONTEXT: Project: {projectName} — {projectDesc}\n\n{existingSpecContext}";

        // ── Phase 3: Structure into Spec ────────────────────────────────────────
        var result = await _openRouter.StructureIntoSpecAsync(systemPrompt, messagesToUse);
        await RecordAiUsageAsync("Spec Structuring",
            systemPrompt + string.Join(" ", messagesToUse.Select(m => m.Content)), result.RawJson);

        // ── Self-Review: runs automatically before PO/BA sees the review screen ─
        var rawJsonForReview = result.RawJson;
        if (!string.IsNullOrWhiteSpace(rawJsonForReview))
        {
            try
            {
                var selfReview = await _openRouter.RunSelfReviewAsync(rawJsonForReview);
                result.SelfReviewResult = selfReview;
                await RecordAiUsageAsync("Quality Self-Review", rawJsonForReview,
                    System.Text.Json.JsonSerializer.Serialize(selfReview));

                // If the LLM applied auto-fixes and produced a revised spec, use it
                if (selfReview.RevisedSpec != null && selfReview.AutoFixes.Any())
                {
                    var revised = selfReview.RevisedSpec;
                    result.Title = !string.IsNullOrWhiteSpace(revised.Title) ? revised.Title : result.Title;
                    result.Description = !string.IsNullOrWhiteSpace(revised.Description)
                        ? revised.Description
                        : result.Description;
                    if (revised.AcceptanceCriteria.Any()) result.AcceptanceCriteria = revised.AcceptanceCriteria;
                    if (revised.ScopeTags.Any()) result.ScopeTags = revised.ScopeTags;
                }

                _logger.LogInformation(
                    "Self-review completed for project {ProjectId}: passed={Passed}, autoFixes={AutoFixCount}",
                    request.ProjectId, selfReview.Passed, selfReview.AutoFixes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Self-review failed for project {ProjectId}; continuing without review result.",
                    request.ProjectId);
            }
        }
        else
        {
            _logger.LogWarning(
                "Self-review skipped for project {ProjectId}: no raw JSON available (fallback spec path).",
                request.ProjectId);
        }

        return Ok(result);
    }

    [HttpPost("api/specs/draft/ingest-transcript")]
    public async Task<ActionResult<IngestTranscriptResponseDto>> IngestRawTranscript(
        [FromBody] IngestTranscriptRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.RawTranscript))
        {
            return BadRequest("Raw transcript content cannot be empty.");
        }

        var currentUser = await _currentUser.GetUserAsync();
        if (currentUser == null) return Unauthorized(new { message = "Authentication required." });

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.CreatedByUserId == currentUser.Id);
        if (project == null) return NotFound(new { message = "Project not found or access denied." });

        var projectName = project.Name;
        var projectDesc = project.Description;

        // 1. Index raw transcript into PostgreSQL pgvector store permanently
        await _vectorStore.IndexDocumentAsync(
            request.ProjectId,
            "raw_transcript",
            $"Raw Ingestion [{request.SourceTag}]",
            request.RawTranscript);

        // 2. Build AI prompt to parse and synthesize the transcript
        var analysisMessages = new List<ChatMessageDto>
        {
            new ChatMessageDto
            {
                Role = "user",
                Content =
                    $"Here is the raw meeting transcript / requirements dump ({request.SourceTag}):\n\n```\n{request.RawTranscript}\n```\n\nPlease deeply analyze this text, filter out noise/banter, extract the core technical requirements, actors, acceptance criteria, and edge cases."
            }
        };

        var existingSpecContext = await GetExistingSpecContextAsync(request.ProjectId);

        var systemPrompt = $@"You are a Principal Software Architect and Lead Business Analyst.
Analyze the provided raw meeting notes or MS Teams transcript for Project: '{projectName}' ({projectDesc}).
CONTEXT:
{existingSpecContext}

Extract the key features, workflows, and specifications into an initial draft.";

        var structuredDraft = await _openRouter.StructureIntoSpecAsync(systemPrompt, analysisMessages);

        // 3. Formulate executive markdown summary for chat stream
        var criteriaList = string.Join("\n", structuredDraft.AcceptanceCriteria.Select(c => $" - ✅ {c}"));
        var tagsList = string.Join(", ", structuredDraft.ScopeTags);

        var summaryMarkdown = $@"### 📑 Ingested Raw Transcript ({request.SourceTag})

**Extracted Title:** {structuredDraft.Title}

**Executive Overview & User Story:**
{structuredDraft.Description}

**Key Acceptance Criteria Identified ({structuredDraft.AcceptanceCriteria.Count}):**
{criteriaList}

**Scope Tags:** `[{tagsList}]`

---
*💡 The live draft on the right has been pre-populated with these requirements. You can now brainstorm further, ask clarification questions, or click **Structure & Publish Spec** when ready.*";

        // 4. Append to DB chat history
        var dbSession = await _db.ChatSessions
            .Include(cs => cs.Messages)
            .FirstOrDefaultAsync(cs => cs.ProjectId == request.ProjectId && cs.PersonaMode == "po_brainstorming");

        if (dbSession == null)
        {
            dbSession = new ChatSession
            {
                ProjectId = request.ProjectId,
                PersonaMode = "po_brainstorming",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ChatSessions.Add(dbSession);
            await _db.SaveChangesAsync();
        }

        dbSession.Messages.Add(new ChatMessageRecord
        {
            ChatSessionId = dbSession.Id,
            Role = "user",
            Content =
                $"[Uploaded Raw Transcript - {request.SourceTag}]:\n{request.RawTranscript.Substring(0, Math.Min(500, request.RawTranscript.Length))}...",
            Timestamp = DateTime.UtcNow
        });

        dbSession.Messages.Add(new ChatMessageRecord
        {
            ChatSessionId = dbSession.Id,
            Role = "assistant",
            Content = summaryMarkdown,
            Timestamp = DateTime.UtcNow
        });

        dbSession.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new IngestTranscriptResponseDto
        {
            Success = true,
            SummaryReply = summaryMarkdown,
            ExtractedTitle = structuredDraft.Title,
            ExtractedDescription = structuredDraft.Description,
            ExtractedCriteria = structuredDraft.AcceptanceCriteria,
            ExtractedTags = structuredDraft.ScopeTags
        });
    }


    private static SpecDto MapToSpecDto(Spec spec)
    {
        var publishedVersions = spec.Versions.Where(v => v.VersionNumber > 0).OrderByDescending(v => v.VersionNumber)
            .ToList();

        var activePublishedVersions = publishedVersions.Where(v => !v.IsUndone).ToList();
        var currentVersion = activePublishedVersions.FirstOrDefault() ??
                             spec.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        int currentVersionNumber = activePublishedVersions.Any()
            ? activePublishedVersions.First().VersionNumber
            : (publishedVersions.Any() ? publishedVersions.First().VersionNumber : 1);

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
            RowVersion = spec.RowVersion,
            CreatedByUserId = spec.CreatedByUserId,
            CreatedByDisplayName = spec.CreatedByUser != null ? spec.CreatedByUser.DisplayName : null,
            CreatedByUsername = spec.CreatedByUser != null ? spec.CreatedByUser.Username : null,
            CreatedByAvatarUrl = spec.CreatedByUser != null ? spec.CreatedByUser.AvatarUrl : null,
            CurrentAcceptanceCriteria =
                currentVersion?.AcceptanceCriteria.Select(ac => ac.Text).ToList() ?? new List<string>(),
            CurrentScopeTags = currentVersion?.ScopeTags.Select(st => st.TagName).ToList() ?? new List<string>(),
            Versions = publishedVersions
                .Select(v => new SpecVersionDto
                {
                    Id = v.Id,
                    SpecId = v.SpecId,
                    VersionNumber = v.VersionNumber,
                    Content = v.Content,
                    PublishedAt = v.PublishedAt,
                    AuthorUserId = v.AuthorUserId,
                    AuthorDisplayName = v.AuthorUser != null ? v.AuthorUser.DisplayName : null,
                    AuthorUsername = v.AuthorUser != null ? v.AuthorUser.Username : null,
                    AuthorAvatarUrl = v.AuthorUser != null ? v.AuthorUser.AvatarUrl : null,
                    AcceptanceCriteria = v.AcceptanceCriteria.Select(ac => ac.Text).ToList(),
                    ScopeTags = v.ScopeTags.Select(st => st.TagName).ToList(),
                    SelfReviewJson = v.SelfReviewJson,
                    IsUndone = v.IsUndone
                })
                .ToList()
        };
    }
}
