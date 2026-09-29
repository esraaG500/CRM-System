using System.Collections.Concurrent;
using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Common.Messaging;

/// <summary>A use case input (command or query) returning <typeparamref name="TResult"/>.</summary>
#pragma warning disable CA1040 // Marker interface used for generic dispatch
public interface IRequest<TResult>;
#pragma warning restore CA1040

public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest request, CancellationToken ct);
}

public interface IDispatcher
{
    Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken ct = default);
}

/// <summary>
/// Resolves the handler for a request, runs its FluentValidation validators first and logs the outcome.
/// </summary>
internal sealed class Dispatcher(IServiceProvider services, ILogger<Dispatcher> logger) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public async Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken ct = default)
    {
        var requestType = request.GetType();
        await ValidateAsync(request, requestType, ct);

        var invoker = (HandlerInvoker<TResult>)Invokers.GetOrAdd(requestType, t =>
            Activator.CreateInstance(typeof(HandlerInvoker<,>).MakeGenericType(t, typeof(TResult)))!);

        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await invoker.Invoke(request, services, ct);
            logger.LogDebug("Handled {Request} in {ElapsedMs} ms", requestType.Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogInformation("{Request} failed with {ExceptionType}: {Message}", requestType.Name, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    private async Task ValidateAsync(object request, Type requestType, CancellationToken ct)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
        var validators = services.GetServices(validatorType).Cast<IValidator>().ToList();
        if (validators.Count == 0)
        {
            return;
        }

        var context = new ValidationContext<object>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    /// <summary>Strongly typed bridge from a runtime request type to its handler, created once per request type.</summary>
    private abstract class HandlerInvoker<TResult>
    {
        public abstract Task<TResult> Invoke(IRequest<TResult> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class HandlerInvoker<TRequest, TResult> : HandlerInvoker<TResult> where TRequest : IRequest<TResult>
    {
        public override Task<TResult> Invoke(IRequest<TResult> request, IServiceProvider services, CancellationToken ct) =>
            services.GetRequiredService<IRequestHandler<TRequest, TResult>>().Handle((TRequest)request, ct);
    }
}
