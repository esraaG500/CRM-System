using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Crm.Api.Middleware;
using Crm.Api.Security;
using Crm.Application;
using Crm.Application.Abstractions;
using Crm.Application.Common.Security;
using Crm.Domain.Common;
using Crm.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Crm.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, logger) => logger
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<CurrentUser>();
    builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
    builder.Services.AddScoped<IAuditContext>(sp => sp.GetRequiredService<CurrentUser>());

    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<Language>(JsonNamingPolicy.CamelCase));
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });

    builder.Services.AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new ApiVersion(1, 0);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ReportApiVersions = true;
            o.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(o =>
        {
            o.GroupNameFormat = "'v'V";
            o.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
    if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes. Set it with 'dotnet user-secrets'.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name",
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    builder.Services.AddAuthorization(o =>
    {
        foreach (var permission in Permissions.All)
        {
            o.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim(CrmClaims.Permission, permission));
        }
    });
    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:LoginPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new() { Title = "Customer Support CRM API", Version = "v1" });
        o.AddSecurityDefinition("Bearer", new()
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        });
        o.AddSecurityRequirement(new()
        {
            [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
        });
    });

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        // In Development the Angular dev server proxies /api over plain HTTP; redirecting those calls to
        // https://localhost:7206 would make them cross-origin, dropping the bearer token and refresh cookie.
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    // "dotnet run -- --seed-demo" migrates, seeds reference + demo data, then exits (Development only).
    // "--reset-demo" first removes existing demo records (user-entered data is kept).
    var resetDemo = args.Contains("--reset-demo");
    var seedDemoOnly = resetDemo || args.Contains("--seed-demo");
    if (seedDemoOnly && !app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("--seed-demo and --reset-demo are only allowed in the Development environment.");
    }

    if (seedDemoOnly || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();

        if (seedDemoOnly || (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:DemoData")))
        {
            var demo = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            if (resetDemo)
            {
                await demo.RemoveAsync();
            }

            await demo.SeedAsync();
        }
    }

    if (seedDemoOnly)
    {
        return;
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

internal sealed class DatabaseHealthCheck(CrmDbContext db) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Cannot connect to the database.");
}

/// <summary>Entry point marker for <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program;
