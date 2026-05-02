using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Wealthra.Domain.Enums;

namespace Wealthra.Api.IntegrationTests;

/// <summary>
/// HTTP smoke tests for admin platform, announcements, support tickets, and internal AI usage endpoints.
/// </summary>
public class PlatformEndpointsTests : IClassFixture<WealthraApiFactory>
{
    private readonly HttpClient _client;

    public PlatformEndpointsTests(WealthraApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private sealed record LoginInfo(string Token, string UserId);

    private async Task<string> LoginAsync(string email, string password)
    {
        var info = await LoginWithDetailsAsync(email, password);
        return info.Token;
    }

    private async Task<LoginInfo> LoginWithDetailsAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/Account/login", new { email, password });
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;
        var token = root.GetProperty("token").GetString()
                    ?? throw new InvalidOperationException("Login response missing token.");
        var userId = root.GetProperty("id").GetString()
                     ?? throw new InvalidOperationException("Login response missing id.");
        return new LoginInfo(token, userId);
    }

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private void ClearBearer() => _client.DefaultRequestHeaders.Authorization = null;

    [Fact]
    public async Task AdminPlans_GetPlans_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        var r = await _client.GetAsync("/api/AdminPlans/plans");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task AdminPlans_CreateUpdateDeleteAssignAndUsersByPlan_Workflow_ReturnsExpectedStatuses()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));

        var create = await _client.PostAsJsonAsync("/api/AdminPlans/plans", new
        {
            name = "IntTest Plan",
            description = "Integration test plan",
            monthlyOcrLimit = 10,
            monthlySttLimit = 5,
            monthlyPrice = 9.99m,
            priceCurrency = "USD",
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var planId = await create.Content.ReadFromJsonAsync<int>();

        var update = await _client.PutAsJsonAsync($"/api/AdminPlans/plans/{planId}", new
        {
            id = planId,
            name = "IntTest Plan Updated",
            description = "Updated",
            monthlyOcrLimit = 12,
            monthlySttLimit = 6,
            monthlyPrice = 10m,
            priceCurrency = "USD",
            isActive = true,
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var usersByPlan = await _client.GetAsync($"/api/AdminPlans/plans/{planId}/users");
        Assert.Equal(HttpStatusCode.OK, usersByPlan.StatusCode);

        var assign = await _client.PutAsJsonAsync("/api/AdminPlans/plans/assign", new
        {
            email = "user@wealthra.local",
            planId,
        });
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var delete = await _client.DeleteAsync($"/api/AdminPlans/plans/{planId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task AdminPlans_UsageSummary_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        var r = await _client.GetAsync("/api/AdminPlans/usage/summary");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task AdminUsers_ListAndDetail_ReturnsOk()
    {
        var basicUser = await LoginWithDetailsAsync("user@wealthra.local", "UserPassword123!");
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        var list = await _client.GetAsync("/api/admin/users?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var detail = await _client.GetAsync($"/api/admin/users/{basicUser.UserId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task AdminUsers_AdminActions_ReturnsNoContentOrBadRequest()
    {
        var basicUser = await LoginWithDetailsAsync("user@wealthra.local", "UserPassword123!");
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        var userId = basicUser.UserId;

        var lockResp = await _client.PostAsJsonAsync($"/api/admin/users/{userId}/lock", new { lockout = false });
        Assert.True(lockResp.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest);

        var rolesResp = await _client.PutAsJsonAsync($"/api/admin/users/{userId}/roles", new[] { "Basic" });
        Assert.True(rolesResp.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest);

        var revoke = await _client.PostAsync($"/api/admin/users/{userId}/revoke-sessions", null);
        Assert.True(revoke.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest);

        var pwd = await _client.PostAsJsonAsync($"/api/admin/users/{userId}/password", new { newPassword = "UserPassword123!" });
        Assert.True(pwd.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminSecurity_BlockedIps_CRUD_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));

        var list = await _client.GetAsync("/api/admin/security/blocked-ips");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var block = await _client.PostAsJsonAsync("/api/admin/security/blocked-ips", new
        {
            ipAddress = "203.0.113.50",
            reason = "test",
            expiresUtc = (DateTimeOffset?)null,
        });
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);

        var unblock = await _client.DeleteAsync("/api/admin/security/blocked-ips/203.0.113.50");
        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
    }

    [Fact]
    public async Task AdminFx_ManualRatesAndProviderOrder_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));

        var list = await _client.GetAsync("/api/admin/fx/manual-rates");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var upsert = await _client.PostAsJsonAsync("/api/admin/fx/manual-rates", new
        {
            fromCurrency = "USD",
            toCurrency = "TRY",
            rate = 34.5m,
        });
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var rateId = await upsert.Content.ReadFromJsonAsync<int>();
        Assert.True(rateId > 0);

        var update = await _client.PutAsJsonAsync($"/api/admin/fx/manual-rates/{rateId}", new { rate = 35.1m });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var delete = await _client.DeleteAsync($"/api/admin/fx/manual-rates/{rateId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var getOrder = await _client.GetAsync("/api/admin/fx/provider-order");
        Assert.Equal(HttpStatusCode.OK, getOrder.StatusCode);

        var setOrder = await _client.PutAsJsonAsync("/api/admin/fx/provider-order", new { providerOrderJson = "[]" });
        Assert.Equal(HttpStatusCode.NoContent, setOrder.StatusCode);
    }

    [Fact]
    public async Task AdminAnalytics_RevenueAndGrowth_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/analytics/revenue")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/analytics/growth")).StatusCode);
    }

    [Fact]
    public async Task AdminMonitoring_ErrorsAuditAiUsage_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/monitoring/errors")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/monitoring/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/monitoring/ai-usage?days=7")).StatusCode);
    }

    [Fact]
    public async Task AdminAiSettings_GetAndPut_ReturnsOk()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/settings/ai")).StatusCode);

        var put = await _client.PutAsJsonAsync("/api/admin/settings/ai", new
        {
            enrichmentModel = "test-enrichment",
            defaultChatModel = "test-chat",
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
    }

    [Fact]
    public async Task AdminAnnouncements_ListCreateDelete_Workflow()
    {
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));

        var list = await _client.GetAsync("/api/admin/announcements");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var now = DateTimeOffset.UtcNow;
        var create = await _client.PostAsJsonAsync("/api/admin/announcements", new
        {
            titleEn = "T",
            titleTr = "T",
            bodyEn = "Body",
            bodyTr = "Body",
            severity = AnnouncementSeverity.Info,
            startsAt = now,
            endsAt = now.AddDays(7),
            targetAllSubscribers = true,
            targetPlanIdsJson = (string?)null,
            targetTiersJson = (string?)null,
            isPublished = true,
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = await create.Content.ReadFromJsonAsync<int>();

        var delete = await _client.DeleteAsync($"/api/admin/announcements/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Announcements_Active_AsBasicUser_ReturnsOk()
    {
        ClearBearer();
        SetBearer(await LoginAsync("user@wealthra.local", "UserPassword123!"));
        var r = await _client.GetAsync("/api/Announcements/active");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task SupportTickets_MineCreate_AdminListReply_Workflow()
    {
        ClearBearer();
        SetBearer(await LoginAsync("user@wealthra.local", "UserPassword123!"));

        var mine = await _client.GetAsync("/api/support/tickets/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        var create = await _client.PostAsJsonAsync("/api/support/tickets", new
        {
            subject = "Integration test ticket",
            body = "Please ignore.",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var ticketId = await create.Content.ReadFromJsonAsync<int>();

        ClearBearer();
        SetBearer(await LoginAsync("admin@wealthra.local", "AdminPassword123!"));

        var adminList = await _client.GetAsync("/api/support/tickets/admin?take=50");
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);

        var reply = await _client.PostAsJsonAsync($"/api/support/tickets/{ticketId}/reply", new
        {
            adminReply = "Thanks — closing.",
            status = SupportTicketStatus.Resolved,
        });
        Assert.Equal(HttpStatusCode.NoContent, reply.StatusCode);
    }

    [Fact]
    public async Task InternalAiUsage_Ingest_WithValidKey_ReturnsNoContent_WithoutKey_Unauthorized()
    {
        ClearBearer();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/internal/ai-usage/ingest")
        {
            Content = JsonContent.Create(new
            {
                feature = "Copilot",
                model = "test-model",
                promptTokens = 1,
                completionTokens = 2,
                estimatedCostUsd = 0.0001m,
                userId = (string?)null,
            }),
        };
        req.Headers.Add("X-Internal-Api-Key", WealthraApiFactory.InternalAiUsageKey);
        var ok = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var bad = await _client.PostAsJsonAsync("/api/internal/ai-usage/ingest", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }
}
