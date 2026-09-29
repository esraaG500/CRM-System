using Crm.Domain.Common;

namespace Crm.Application.Auth;

/// <summary>Sign-in and token management. Implemented by the identity system in Infrastructure.</summary>
public interface IAuthService
{
    Task<AuthOutcome> LoginAsync(string email, string password, string? ipAddress, CancellationToken ct);

    Task<AuthOutcome> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct);

    Task LogoutAsync(string refreshToken, CancellationToken ct);

    Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct);
}

public sealed record CurrentUserDto(
    Guid Id,
    string FullName,
    string Email,
    string UserType,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> DepartmentIds,
    Guid? BranchId,
    Language PreferredLanguage);

public sealed record AuthTokens(string AccessToken, int ExpiresIn, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public enum AuthFailure { InvalidCredentials, LockedOut, Inactive, InvalidRefreshToken }

public sealed record AuthOutcome(AuthTokens? Tokens, CurrentUserDto? User, AuthFailure? Failure)
{
    public bool Succeeded => Tokens is not null;

    public static AuthOutcome Success(AuthTokens tokens, CurrentUserDto user) => new(tokens, user, null);

    public static AuthOutcome Fail(AuthFailure failure) => new(null, null, failure);
}
