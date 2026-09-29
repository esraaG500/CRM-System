using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Crm.Application.Abstractions;
using Crm.Application.Auth;
using Crm.Domain.Common;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "crm";
    public string Audience { get; set; } = "crm";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public static class CrmClaims
{
    public const string Permission = "perm";
    public const string Department = "dept";
    public const string Branch = "branch";
    public const string Language = "lang";
}

internal sealed class AuthService(
    CrmDbContext db,
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> options,
    IClock clock,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwt = options.Value;

    public async Task<AuthOutcome> LoginAsync(string email, string password, string? ipAddress, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await AuditSignIn(null, email, "SignInFailed", ipAddress, ct);
            return AuthOutcome.Fail(AuthFailure.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await AuditSignIn(user.Id, email, "SignInFailed", ipAddress, ct);
            return AuthOutcome.Fail(AuthFailure.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            await AuditSignIn(user.Id, email, "SignInFailed", ipAddress, ct);
            logger.LogWarning("Failed sign-in for user {UserId} from {IpAddress}", user.Id, ipAddress);
            return AuthOutcome.Fail(AuthFailure.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            await AuditSignIn(user.Id, email, "SignInFailed", ipAddress, ct);
            return AuthOutcome.Fail(AuthFailure.Inactive);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        user.LastSignInAt = clock.UtcNow;
        await AuditSignIn(user.Id, email, "SignIn", ipAddress, ct);
        return await IssueTokens(user, ipAddress, replacing: null, ct);
    }

    public async Task<AuthOutcome> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = clock.UtcNow;

        if (stored is null)
        {
            return AuthOutcome.Fail(AuthFailure.InvalidRefreshToken);
        }

        if (!stored.IsActive(now))
        {
            // A revoked token being reused suggests theft: revoke the whole chain for this user.
            if (stored.RevokedAt is not null && stored.ReplacedByTokenId is not null)
            {
                logger.LogWarning("Reuse of revoked refresh token for user {UserId}; revoking all sessions", stored.UserId);
                await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            }

            return AuthOutcome.Fail(AuthFailure.InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
        {
            return AuthOutcome.Fail(AuthFailure.Inactive);
        }

        return await IssueTokens(user, ipAddress, stored, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var now = clock.UtcNow;
        await db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Departments).FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : await ToDto(user, ct);
    }

    private async Task<AuthOutcome> IssueTokens(ApplicationUser user, string? ipAddress, RefreshToken? replacing, CancellationToken ct)
    {
        await db.Entry(user).Collection(u => u.Departments).LoadAsync(ct);
        var dto = await ToDto(user, ct);
        var now = clock.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(CrmClaims.Language, dto.PreferredLanguage.ToString()),
        };
        claims.AddRange(dto.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(dto.Permissions.Select(p => new Claim(CrmClaims.Permission, p)));
        claims.AddRange(dto.DepartmentIds.Select(d => new Claim(CrmClaims.Department, d.ToString())));
        if (user.BranchId is { } branch)
        {
            claims.Add(new Claim(CrmClaims.Branch, branch.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var token = new JwtSecurityToken(
            _jwt.Issuer, _jwt.Audience, claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_jwt.AccessTokenMinutes).UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var refreshValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(refreshValue),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays),
            CreatedIp = ipAddress,
        };
        db.RefreshTokens.Add(refresh);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByTokenId = refresh.Id;
        }

        await db.SaveChangesAsync(ct);

        var tokens = new AuthTokens(
            new JwtSecurityTokenHandler().WriteToken(token), _jwt.AccessTokenMinutes * 60, refreshValue, refresh.ExpiresAt);
        return AuthOutcome.Success(tokens, dto);
    }

    private async Task<CurrentUserDto> ToDto(ApplicationUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await db.RolePermissions
            .Where(p => db.UserRoles.Any(ur => ur.UserId == user.Id && ur.RoleId == p.RoleId))
            .Select(p => p.Permission).Distinct().OrderBy(p => p).ToListAsync(ct);

        return new CurrentUserDto(
            user.Id, user.FullName, user.Email ?? string.Empty, user.UserType.ToString(),
            roles.ToList(), permissions, user.Departments.Select(d => d.DepartmentId).ToList(),
            user.BranchId, user.PreferredLanguage);
    }

    private async Task AuditSignIn(Guid? userId, string email, string action, string? ip, CancellationToken ct)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            Timestamp = clock.UtcNow,
            UserId = userId,
            Action = action,
            EntityType = "User",
            EntityId = userId?.ToString(),
            Changes = System.Text.Json.JsonSerializer.Serialize(new { email }),
            IpAddress = ip,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
