using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Domain.Common;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.MsSql;

namespace Crm.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API against a real SQL Server database (never the EF in-memory provider).
/// Uses LocalDB by default; set <c>CRM_TEST_USE_DOCKER=1</c> to use a Testcontainers SQL Server instead.
/// </summary>
public sealed class CrmApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Test-Password-123";
    public const string AdminEmail = "admin@crm.local";
    public const string SupervisorEmail = "supervisor@crm.local";
    public const string SupportAgentEmail = "agent1@crm.local";
    public const string TechnicalAgentEmail = "agent3@crm.local";

    public static readonly JsonSerializerOptions Json = CreateJson();

    private MsSqlContainer? _container;
    private Respawner? _respawner;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("CRM_TEST_USE_DOCKER") == "1")
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync();
            _connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "CrmTests" }.ConnectionString;
        }
        else
        {
            _connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database=CrmTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
        }

        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToInclude = ["Customers", "ContactPeople", "Notes", "Tickets", "TicketHistory", "Messages", "MessageMentions", "AuditLog", "RefreshTokens"],
        });
    }

    public async Task ResetDataAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    public async Task<HttpClient> CreateClientAs(string email)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, Json);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    public async Task<T> WithDb<T>(Func<CrmDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<CrmDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting (unlike ConfigureAppConfiguration) is visible to Program.cs before the host is built.
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:Crm"] = _connectionString,
            ["Jwt:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
            ["Seed:AdminPassword"] = Password,
            ["Seed:SampleData"] = "true",
            ["Seed:SamplePassword"] = Password,
            ["Database:MigrateOnStartup"] = "false",
            ["RateLimits:LoginPerMinute"] = "10000",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        };
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    public new async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
        else
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<Language>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<CrmApiFactory>
{
    public const string Name = "api";
}

/// <summary>Base class: every test starts with empty customer/ticket data but the seeded users and reference data.</summary>
[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(CrmApiFactory factory) : IAsyncLifetime
{
    protected CrmApiFactory Factory { get; } = factory;
    protected static JsonSerializerOptions Json => CrmApiFactory.Json;

    public Task InitializeAsync() => Factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
