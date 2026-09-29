namespace Crm.Domain.Common;

/// <summary>A business rule was violated. <see cref="Code"/> is a stable machine-readable code.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>The operation conflicts with the current state of the record (HTTP 409).</summary>
public sealed class ConflictException(string code, string message) : DomainException(code, message);
