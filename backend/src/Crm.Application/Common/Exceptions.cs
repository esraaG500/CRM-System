namespace Crm.Application.Common;

/// <summary>The record does not exist or is outside the caller's data scope (HTTP 404).</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;
}

/// <summary>The caller is authenticated but not allowed to perform the action (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);
