using Crm.Application.Abstractions;
using Crm.Application.Auth;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Interceptors;
using Crm.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Crm")
            ?? throw new InvalidOperationException("Connection string 'Crm' is not configured.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<CrmDbContext>((sp, options) => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CrmDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
