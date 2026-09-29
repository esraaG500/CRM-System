using System.Reflection;
using System.Text.Json;
using Crm.Application.Abstractions;
using Crm.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>Correlation data for the audit log, supplied per request by the API layer.</summary>
public interface IAuditContext
{
    string? IpAddress { get; }
    string? CorrelationId { get; }
}

/// <summary>
/// Sets auditable timestamps and writes an <see cref="AuditLogEntry"/> with before/after values for every
/// create, update or delete of an entity marked <see cref="AuditedAttribute"/>.
/// </summary>
internal sealed class AuditInterceptor(ICurrentUser currentUser, IClock clock, IAuditContext auditContext) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            Apply(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            Apply(context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var audit = new List<AuditLogEntry>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        if (auditable.CreatedAt == default)
                        {
                            auditable.CreatedAt = now;
                        }

                        auditable.CreatedBy ??= userId;
                        break;
                    case EntityState.Modified:
                        auditable.UpdatedAt = now;
                        auditable.UpdatedBy = userId;
                        break;
                }
            }

            if (entry.Entity.GetType().GetCustomAttribute<AuditedAttribute>() is null
                || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                _ when entry.Entity is ISoftDelete { IsDeleted: true } && entry.Property(nameof(ISoftDelete.IsDeleted)).IsModified => "Delete",
                _ => "Update",
            };

            var changes = Describe(entry);
            if (action == "Update" && changes.Count == 0)
            {
                continue;
            }

            audit.Add(new AuditLogEntry
            {
                Timestamp = now,
                UserId = userId,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                Changes = JsonSerializer.Serialize(changes, Json),
                IpAddress = auditContext.IpAddress,
                CorrelationId = auditContext.CorrelationId,
            });
        }

        context.Set<AuditLogEntry>().AddRange(audit);
    }

    private static Dictionary<string, object?> Describe(EntityEntry entry)
    {
        var result = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (name is nameof(IHasRowVersion.RowVersion) or nameof(AuditableEntity.UpdatedAt) or nameof(AuditableEntity.UpdatedBy))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    result[name] = property.CurrentValue;
                    break;
                case EntityState.Deleted:
                    result[name] = property.OriginalValue;
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    result[name] = new { before = property.OriginalValue, after = property.CurrentValue };
                    break;
            }
        }

        return result;
    }
}
