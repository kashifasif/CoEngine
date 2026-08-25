using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpecPlatform.Api.Data;
using SpecPlatform.Shared.DTOs;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectsController(AppDbContext db)
    {
        _db = db;
    }

    private async Task<User?> GetCurrentAuthenticatedUserAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (token.StartsWith("gh_session_"))
            {
                var parts = token.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[2], out var userId))
                {
                    return await _db.Users.FindAsync(userId);
                }
            }
        }

        return await _db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetProjects()
    {
        var projects = await _db.Projects
            .Include(p => p.CreatedByUser)
            .Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                SpecCount = p.Specs.Count,
                CreatedByUserId = p.CreatedByUserId,
                CreatedByDisplayName = p.CreatedByUser != null ? p.CreatedByUser.DisplayName : null,
                CreatedByUsername = p.CreatedByUser != null ? p.CreatedByUser.Username : null,
                CreatedByAvatarUrl = p.CreatedByUser != null ? p.CreatedByUser.AvatarUrl : null
            })
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> GetProject(int id)
    {
        var project = await _db.Projects
            .Include(p => p.Specs)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return NotFound(new { message = $"Project {id} not found." });
        }

        return Ok(new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            SpecCount = project.Specs.Count,
            CreatedByUserId = project.CreatedByUserId,
            CreatedByDisplayName = project.CreatedByUser?.DisplayName,
            CreatedByUsername = project.CreatedByUser?.Username,
            CreatedByAvatarUrl = project.CreatedByUser?.AvatarUrl
        });
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] CreateProjectDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Project Name is required." });
        }

        var currentUser = await GetCurrentAuthenticatedUserAsync();
        var project = new Project
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            CreatedByUserId = currentUser?.Id,
            CreatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var result = new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            CreatedAt = project.CreatedAt,
            SpecCount = 0,
            CreatedByUserId = currentUser?.Id,
            CreatedByDisplayName = currentUser?.DisplayName ?? currentUser?.Username,
            CreatedByUsername = currentUser?.Username,
            CreatedByAvatarUrl = currentUser?.AvatarUrl
        };

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, result);
    }
}
