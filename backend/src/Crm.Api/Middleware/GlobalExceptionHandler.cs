using Crm.Application.Common;
using Crm.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Middleware;

/// <summary>Maps exceptions to RFC 7807 Problem Details without leaking internals.</summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException ex => Validation(ex),
            NotFoundException ex => Problem(StatusCodes.Status404NotFound, "Not found", ex.Message, "not-found"),
            ForbiddenException ex => Problem(StatusCodes.Status403Forbidden, "Forbidden", ex.Message, "forbidden"),
            UnauthorizedAccessException => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", "Authentication required.", "unauthorized"),
            ConflictException ex => Problem(StatusCodes.Status409Conflict, "Conflict", ex.Message, ex.Code),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Conflict",
                "This record was changed by someone else. Reload it and try again.", "concurrency.stale"),
            DomainException ex => Problem(StatusCodes.Status400BadRequest, "Business rule violated", ex.Message, ex.Code),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => null,
            _ => null,
        };

        if (problem is null)
        {
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = 499;
                return true;
            }

            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Server error",
                "An unexpected error occurred. Quote the correlation ID when contacting support.", "server-error");
        }

        problem.Extensions["correlationId"] = context.TraceIdentifier;
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }

    private static ProblemDetails Problem(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return problem;
    }

    private static ValidationProblemDetails Validation(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more fields are invalid.",
        };
        problem.Extensions["code"] = "validation";
        return problem;
    }

    /// <summary>"Customer.PrimaryEmail" → "primaryEmail" so errors match the JSON field names clients send.</summary>
    private static string ToCamelCase(string propertyName)
    {
        var last = propertyName.Split('.')[^1];
        return string.IsNullOrEmpty(last) ? string.Empty : char.ToLowerInvariant(last[0]) + last[1..];
    }
}
