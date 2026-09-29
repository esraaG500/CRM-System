using NetArchTest.Rules;

namespace Crm.Architecture.Tests;

/// <summary>Enforces the Clean Architecture dependency rule (constitution Principle I).</summary>
public class LayerDependencyTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(Domain.Common.Entity).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly Api = typeof(Api.Controllers.V1.ApiControllerBase).Assembly;

    [Fact]
    public void Domain_has_no_framework_or_outer_layer_dependencies()
    {
        var result = Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Crm.Application", "Crm.Infrastructure", "Crm.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("Crm.Infrastructure", "Crm.Api", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Controllers_do_not_use_the_database_directly()
    {
        var result = Types.InAssembly(Api).That().ResideInNamespace("Crm.Api.Controllers").ShouldNot()
            .HaveDependencyOnAny("Crm.Infrastructure.Persistence", "Microsoft.EntityFrameworkCore", "Crm.Application.Abstractions.IAppDbContext")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
