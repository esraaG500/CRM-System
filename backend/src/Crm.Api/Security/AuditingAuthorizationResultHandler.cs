using Crm.Application.Abstractions;
using Crm.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Crm.Api.Security;

/// <summary>Writes a <c>PermissionDenied</c> audit entry whenever an authenticated user is refused access.</summary>
internal sealed class AuditingAuthorizationResultHandler(ILogger<AuditingAuthorizationResultHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var user = context.RequestServices.GetRequiredService<ICurrentUser>();
            var db = context.RequestServices.GetRequiredService<IAppDbContext>();
            var clock = context.RequestServices.GetRequiredService<IClock>();
            var requirement = string.Join(",", policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.ClaimsAuthorizationRequirement>()
                .SelectMany(r => r.AllowedValues ?? []));

            logger.LogWarning("Permission denied for {UserId} on {Method} {Path} (requires {Permission})",
                user.UserId, context.Request.Method, context.Request.Path, requirement);

            db.AuditLog.Add(new AuditLogEntry
            {
                Timestamp = clock.UtcNow,
                UserId = user.UserId,
                Action = "PermissionDenied",
                EntityType = "Endpoint",
                EntityId = $"{context.Request.Method} {context.Request.Path}",
                Changes = System.Text.Json.JsonSerializer.Serialize(new { requires = requirement }),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                CorrelationId = context.TraceIdentifier,
            });
            await db.SaveChangesAsync(context.RequestAborted);
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
