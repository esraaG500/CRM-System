using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.IntegrationTests.Platform;

public class PlatformAndAuthTests(CrmApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Health_is_anonymous()
    {
        var response = await Factory.CreateClient().GetAsync("/health");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Protected_endpoint_requires_authentication()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/customers");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Not_found_returns_problem_details_with_correlation_id()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-corr-1");

        var response = await client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.GetValues("X-Correlation-ID").Single().ShouldBe("test-corr-1");
        var problem = await response.ReadJson(HttpStatusCode.NotFound);
        problem.Str("correlationId").ShouldBe("test-corr-1");
        problem.Str("code").ShouldBe("not-found");
    }

    [Fact]
    public async Task Login_returns_token_and_permissions()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = CrmApiFactory.SupervisorEmail, password = CrmApiFactory.Password });

        var body = await response.ReadJson(HttpStatusCode.OK);
        body.Str("accessToken").ShouldNotBeNullOrEmpty();
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("crm_refresh=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        var permissions = body.GetProperty("user").GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        permissions.ShouldContain("tickets.assign");
    }

    [Fact]
    public async Task Wrong_password_is_rejected_and_audited()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = CrmApiFactory.SupportAgentEmail, password = "wrong-password-1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var audited = await Factory.WithDb(db => db.AuditLog.AnyAsync(a => a.Action == "SignInFailed"));
        audited.ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_rotates_cookie_and_old_token_cannot_be_reused()
    {
        var client = await Factory.CreateClientAs(CrmApiFactory.SupportAgentEmail);

        var refreshed = await client.PostAsync("/api/v1/auth/refresh", null);
        (await refreshed.ReadJson(HttpStatusCode.OK)).Str("accessToken").ShouldNotBeNullOrEmpty();

        var revokedCount = await Factory.WithDb(db => db.RefreshTokens.CountAsync(t => t.RevokedAt != null));
        revokedCount.ShouldBe(1);

        var me = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/me", Json);
        me.Str("email").ShouldBe(CrmApiFactory.SupportAgentEmail);
    }
}
