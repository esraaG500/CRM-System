using Crm.Application.Common.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crm.Application.UnitTests.Common;

public class DispatcherTests
{
    public sealed record Ping(string Text) : IRequest<string>;

    private sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public int Calls { get; private set; }

        public Task<string> Handle(Ping request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult($"pong:{request.Text}");
        }
    }

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(x => x.Text).NotEmpty();
    }

    private static (Dispatcher Dispatcher, PingHandler Handler) Create()
    {
        var handler = new PingHandler();
        var services = new ServiceCollection()
            .AddSingleton<IRequestHandler<Ping, string>>(handler)
            .AddSingleton<IValidator<Ping>, PingValidator>()
            .BuildServiceProvider();
        return (new Dispatcher(services, NullLogger<Dispatcher>.Instance), handler);
    }

    [Fact]
    public async Task Dispatches_to_the_registered_handler()
    {
        var (dispatcher, _) = Create();

        (await dispatcher.Send(new Ping("hi"))).ShouldBe("pong:hi");
    }

    [Fact]
    public async Task Runs_validators_before_the_handler()
    {
        var (dispatcher, handler) = Create();

        var ex = await Should.ThrowAsync<ValidationException>(() => dispatcher.Send(new Ping("")));

        ex.Errors.ShouldContain(e => e.PropertyName == "Text");
        handler.Calls.ShouldBe(0);
    }
}
