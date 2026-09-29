using Crm.Application.Common.Security;
using Crm.Domain.Common;
using Crm.Domain.Organization;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Persistence.Seed;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    public string AdminEmail { get; set; } = "admin@crm.local";
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Creates sample departments, categories and agents. Development only.</summary>
    public bool SampleData { get; set; }

    public string SamplePassword { get; set; } = string.Empty;

    /// <summary>Also creates demo customers and tickets on startup (see <see cref="DemoDataSeeder"/>). Development only.</summary>
    public bool DemoData { get; set; }
}

/// <summary>Idempotent seeding of system roles, permissions, reference data and the first administrator.</summary>
public sealed class DatabaseSeeder(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRoles();
        var departments = await SeedReferenceData(ct);
        await SeedAdmin(departments);

        if (_options.SampleData)
        {
            await SeedSampleAgents(departments);
        }
    }

    private async Task SeedRoles()
    {
        var roles = new Dictionary<string, IReadOnlyList<string>>
        {
            [SystemRoles.Administrator] = Permissions.All,
            [SystemRoles.Supervisor] = Permissions.Supervisor,
            [SystemRoles.Agent] = Permissions.Agent,
            [SystemRoles.Customer] = [],
        };

        foreach (var (name, permissions) in roles)
        {
            var role = await roleManager.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == name);
            if (role is null)
            {
                role = new ApplicationRole(name) { IsSystem = true };
                Check(await roleManager.CreateAsync(role), $"create role {name}");
            }

            // System roles always get newly introduced permissions.
            foreach (var permission in permissions.Where(p => role.Permissions.TrueForAll(rp => rp.Permission != p)))
            {
                role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
            }

            await db.SaveChangesAsync();
        }
    }

    private async Task<List<Department>> SeedReferenceData(CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(ct))
        {
            db.Branches.Add(new Branch("Riyadh", "الرياض"));
        }

        if (!await db.Departments.AnyAsync(ct))
        {
            var support = new Department("Customer Support", "خدمة العملاء");
            var billing = new Department("Billing", "الفوترة");
            var technical = new Department("Technical Support", "الدعم الفني");
            db.Departments.AddRange(support, billing, technical);

            db.Categories.AddRange(
                new Category("General inquiry", "استفسار عام", support.Id, 1),
                new Category("Complaint", "شكوى", support.Id, 2),
                new Category("Billing & payments", "الفواتير والمدفوعات", billing.Id, 3),
                new Category("Technical issue", "مشكلة تقنية", technical.Id, 4),
                new Category("Feature request", "طلب ميزة", technical.Id, 5));
        }

        await db.SaveChangesAsync(ct);
        return await db.Departments.OrderBy(d => d.NameEn).ToListAsync(ct);
    }

    private async Task SeedAdmin(List<Department> departments)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminPassword) || await userManager.FindByEmailAsync(_options.AdminEmail) is not null)
        {
            return;
        }

        await CreateUser(_options.AdminEmail, "System Administrator", _options.AdminPassword, SystemRoles.Administrator, departments);
        logger.LogInformation("Created administrator {Email}", _options.AdminEmail);
    }

    private async Task SeedSampleAgents(List<Department> departments)
    {
        if (string.IsNullOrWhiteSpace(_options.SamplePassword))
        {
            return;
        }

        var support = departments.First(d => d.NameEn == "Customer Support");
        var technical = departments.First(d => d.NameEn == "Technical Support");
        var billing = departments.First(d => d.NameEn == "Billing");

        var supervisor = await CreateUser("supervisor@crm.local", "Noura Al-Qahtani", _options.SamplePassword, SystemRoles.Supervisor, departments);
        await CreateUser("agent1@crm.local", "Omar Al-Harbi", _options.SamplePassword, SystemRoles.Agent, [support]);
        await CreateUser("agent2@crm.local", "Layla Hassan", _options.SamplePassword, SystemRoles.Agent, [support, billing]);
        await CreateUser("agent3@crm.local", "Faisal Al-Otaibi", _options.SamplePassword, SystemRoles.Agent, [technical]);

        if (supervisor is not null)
        {
            foreach (var d in departments.Where(d => d.EscalationOwnerId is null))
            {
                d.SetEscalationOwner(supervisor.Id);
            }

            await db.SaveChangesAsync();
        }
    }

    private async Task<ApplicationUser?> CreateUser(string email, string name, string password, string role, IEnumerable<Department> departments)
    {
        if (await userManager.FindByEmailAsync(email) is { } existing)
        {
            return existing;
        }

        var branchId = await db.Branches.Select(b => (Guid?)b.Id).FirstOrDefaultAsync();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            BranchId = branchId,
            PreferredLanguage = Language.En,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        user.Departments.AddRange(departments.Select(d => new UserDepartment { UserId = user.Id, DepartmentId = d.Id }));

        Check(await userManager.CreateAsync(user, password), $"create user {email}");
        Check(await userManager.AddToRoleAsync(user, role), $"add {email} to {role}");
        return user;
    }

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Seeding failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
