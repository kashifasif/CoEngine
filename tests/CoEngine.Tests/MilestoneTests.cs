using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoEngine.Api.Data;
using CoEngine.Shared.DTOs;
using Xunit;

using CoEngine.Shared.Models;

namespace CoEngine.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDbName = $"TestDb_{Guid.NewGuid():N}";
    public Guid SeedUserId { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all EF Core registrations from the real Program.cs
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                (d.ServiceType.FullName != null && d.ServiceType.FullName.Contains("EntityFrameworkCore"))
            ).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Use EF Core InMemory for tests — no PostgreSQL required
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_testDbName));

            try
            {
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();

                // Seed authentic user
                db.Users.Add(new User
                {
                    Id = SeedUserId,
                    GitHubId = "12345678",
                    Username = "github_developer",
                    DisplayName = "GitHub Developer",
                    Email = "developer@coengine.io",
                    AvatarUrl = "https://github.com/identicons/coengine.png",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TEST FACTORY INIT ERROR]: {ex}");
                throw;
            }
        });
    }
}

public class MilestoneTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MilestoneTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Cookie", $"coengine_session=gh_session_{factory.SeedUserId}_{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task Milestone0_HealthCheck_Returns200OK_And_AliveMessage()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("API is alive", health.Message);
    }

    [Fact]
    public async Task Milestone1_Project_And_Spec_CRUD_And_Version_Isolation()
    {
        var proj1Res = await _client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = "Loan Approval Feature",
            Description = "Automated credit risk decisioning engine"
        });
        Assert.Equal(HttpStatusCode.Created, proj1Res.StatusCode);
        var proj1 = await proj1Res.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(proj1);

        var proj2Res = await _client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = "PrivateLendingCommon MFE",
            Description = "Shared microfrontend components"
        });
        Assert.Equal(HttpStatusCode.Created, proj2Res.StatusCode);
        var proj2 = await proj2Res.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(proj2);

        var spec1Res = await _client.PostAsJsonAsync($"/api/projects/{proj1.Id}/specs", new CreateSpecDto
        {
            Title = "Risk Rule Engine API",
            Description = "Evaluates borrower risk tier",
            AcceptanceCriteria = new List<string> { "Calculates DTI score", "Rejects DTI > 50%" },
            ScopeTags = new List<string> { "api", "bff" }
        });
        Assert.Equal(HttpStatusCode.Created, spec1Res.StatusCode);
        var spec1 = await spec1Res.Content.ReadFromJsonAsync<SpecDto>();
        Assert.NotNull(spec1);
        Assert.Equal(proj1.Id, spec1.ProjectId);
        Assert.Equal("Draft", spec1.Status);

        var proj2Specs = await _client.GetFromJsonAsync<List<SpecDto>>($"/api/projects/{proj2.Id}/specs");
        Assert.NotNull(proj2Specs);
        Assert.Empty(proj2Specs);

        var pub1Res = await _client.PostAsync($"/api/specs/{spec1.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, pub1Res.StatusCode);
        var pub1Result = await pub1Res.Content.ReadFromJsonAsync<PublishResultDto>();
        Assert.NotNull(pub1Result);
        Assert.Equal("Published", pub1Result.Spec.Status);
        Assert.Equal(1, pub1Result.Spec.CurrentVersionNumber);

        await _client.PutAsJsonAsync($"/api/specs/{spec1.Id}", new UpdateSpecDto
        {
            Title = "Risk Rule Engine API v2",
            Description = "Evaluates borrower risk tier with updated limits",
            AcceptanceCriteria = new List<string> { "Calculates DTI score", "Rejects DTI > 50%", "Supports collateral override" },
            ScopeTags = new List<string> { "api", "bff", "v2" }
        });

        var pub2Res = await _client.PostAsync($"/api/specs/{spec1.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, pub2Res.StatusCode);
        var pub2Result = await pub2Res.Content.ReadFromJsonAsync<PublishResultDto>();
        Assert.NotNull(pub2Result);
        Assert.Equal(2, pub2Result.Spec.CurrentVersionNumber);
        Assert.Equal(2, pub2Result.Spec.Versions.Count);
    }

    [Fact]
    public async Task Milestone2_AiBrainstorming_Chat_And_Structure_Workflows()
    {
        var projRes = await _client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = "Daily Standup Bot",
            Description = "Automated Slack/Teams standup summary bot"
        });
        var proj = await projRes.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(proj);

        var chatRes = await _client.PostAsJsonAsync("/api/specs/draft/chat", new ChatRequestDto
        {
            ProjectId = proj.Id,
            Messages = new List<ChatMessageDto>
            {
                new ChatMessageDto { Role = "user", Content = "We need a standup bot that prompts users at 9 AM." }
            }
        });
        Assert.Equal(HttpStatusCode.OK, chatRes.StatusCode);
        var chatResult = await chatRes.Content.ReadFromJsonAsync<ChatResponseDto>();
        Assert.NotNull(chatResult);
        Assert.True(chatResult.Success);
        Assert.NotEmpty(chatResult.Reply);

        var structRes = await _client.PostAsJsonAsync("/api/specs/draft/structure", new ChatRequestDto
        {
            ProjectId = proj.Id,
            Messages = new List<ChatMessageDto>
            {
                new ChatMessageDto { Role = "user", Content = "We need a standup bot that prompts users at 9 AM." }
            }
        });
        Assert.Equal(HttpStatusCode.OK, structRes.StatusCode);
        var structResult = await structRes.Content.ReadFromJsonAsync<StructuredSpecResultDto>();
        Assert.NotNull(structResult);
        Assert.NotEmpty(structResult.Title);
        Assert.NotEmpty(structResult.AcceptanceCriteria);
    }

    [Fact]
    public async Task Milestone4_Publish_Generates_Notification_Summary()
    {
        var projRes = await _client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = "Notification Test Project",
            Description = "Test project"
        });
        var proj = await projRes.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(proj);

        var specRes = await _client.PostAsJsonAsync($"/api/projects/{proj.Id}/specs", new CreateSpecDto
        {
            Title = "Notification Spec",
            Description = "Spec description",
            AcceptanceCriteria = new List<string> { "Criterion 1", "Criterion 2" },
            ScopeTags = new List<string> { "tag1" }
        });
        var spec = await specRes.Content.ReadFromJsonAsync<SpecDto>();
        Assert.NotNull(spec);

        var pubRes = await _client.PostAsync($"/api/specs/{spec.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);
        var pubResult = await pubRes.Content.ReadFromJsonAsync<PublishResultDto>();
        Assert.NotNull(pubResult);
        Assert.Contains("[NOTIFICATION]", pubResult.NotificationSummary);
        Assert.Contains("Notification Spec", pubResult.NotificationSummary);

        var notifs = await _client.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.NotNull(notifs);
        Assert.NotEmpty(notifs);
        Assert.Contains(notifs, n => n.SpecId == spec.Id);
    }

    [Fact]
    public async Task Milestone4_VersionDiff_Calculates_Added_And_Removed_Items()
    {
        var projRes = await _client.PostAsJsonAsync("/api/projects", new CreateProjectDto
        {
            Name = "Diff Project",
            Description = "Diff test"
        });
        var proj = await projRes.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(proj);

        var specRes = await _client.PostAsJsonAsync($"/api/projects/{proj.Id}/specs", new CreateSpecDto
        {
            Title = "Diff Spec",
            Description = "Initial",
            AcceptanceCriteria = new List<string> { "Base Criterion" },
            ScopeTags = new List<string> { "v1tag" }
        });
        var spec = await specRes.Content.ReadFromJsonAsync<SpecDto>();
        Assert.NotNull(spec);

        // Publish v1
        await _client.PostAsync($"/api/specs/{spec.Id}/publish", null);

        // Update draft and publish v2
        await _client.PutAsJsonAsync($"/api/specs/{spec.Id}", new UpdateSpecDto
        {
            Title = "Diff Spec Updated",
            Description = "Updated",
            AcceptanceCriteria = new List<string> { "Base Criterion", "New Criterion Added" },
            ScopeTags = new List<string> { "v1tag", "v2tag" }
        });
        await _client.PostAsync($"/api/specs/{spec.Id}/publish", null);

        // Call Diff endpoint
        var diff = await _client.GetFromJsonAsync<SpecDiffDto>($"/api/specs/{spec.Id}/versions/1/diff/2");
        Assert.NotNull(diff);
        Assert.Equal(1, diff.FromVersion);
        Assert.Equal(2, diff.ToVersion);
        Assert.Contains("New Criterion Added", diff.AddedCriteria);
        Assert.Contains("v2tag", diff.AddedScopeTags);
    }

    [Fact]
    public async Task Milestone5_GitHub_SSO_Auth_Flow()
    {
        // 1. Fetch GitHub Authorization URL
        var authUrlRes = await _client.GetFromJsonAsync<GitHubAuthUrlDto>("/api/auth/github/url?redirectUri=http://localhost:5005/auth/github/callback");
        Assert.NotNull(authUrlRes);
        Assert.Contains("github.com/login/oauth/authorize", authUrlRes.AuthUrl);
        Assert.Contains("client_id=", authUrlRes.AuthUrl);

        // 2. Process OAuth Callback with Dev/Test Mock Code
        var callbackRes = await _client.PostAsJsonAsync("/api/auth/github/callback", new GitHubCallbackRequestDto
        {
            Code = "test_mock_oauth_code_12345"
        });
        Assert.Equal(HttpStatusCode.OK, callbackRes.StatusCode);

        var user = await callbackRes.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.True(user.IsAuthenticated);
        Assert.Equal("github_developer", user.Username);

        // 3. Verify /api/auth/me returns user profile
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Add("Cookie", $"coengine_session=gh_session_{user.Id}_{Guid.NewGuid():N}");
        var meRes = await _client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var meUser = await meRes.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(meUser);
        Assert.True(meUser.IsAuthenticated);
        Assert.Equal("github_developer", meUser.Username);

        // 4. Verify Logout
        var logoutRes = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);
    }

    [Fact]
    public async Task Milestone6_Unauthenticated_Request_Returns_401Unauthorized()
    {
        using var unauthClient = _factory.CreateClient();
        unauthClient.DefaultRequestHeaders.Clear();
        var response = await unauthClient.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
