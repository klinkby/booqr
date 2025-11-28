using System.Reflection;
using NetArchTest.Rules;

namespace Klinkby.Booqr.Tests;

/// <summary>
/// Architecture tests to enforce structural policies as defined in ARCHITECTURE.md
/// </summary>
public class ArchitectureTests
{
    private const string ApplicationNamespace = "Klinkby.Booqr.Application";
    private const string InfrastructureNamespace = "Klinkby.Booqr.Infrastructure";
    private const string ApiNamespace = "Klinkby.Booqr.Api";

    private static Assembly CoreAssembly => typeof(Core.IId).Assembly;
    private static Assembly ApplicationAssembly => typeof(Application.Abstractions.ICommand<>).Assembly;
    private static Assembly InfrastructureAssembly => Assembly.Load("Klinkby.Booqr.Infrastructure");
    private static Assembly ApiAssembly => Assembly.Load("Klinkby.Booqr.Api");

    [Fact]
    public void Core_ShouldOnlyReferenceSystemAssemblies()
    {
        // Core should only reference System.* assemblies
        var result = Types
            .InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, GetFailureMessage(result, "Core should only reference System.* assemblies"));
    }

    [Fact]
    public void Core_ShouldOnlyContainRecordsInterfacesExceptionsAndStaticClasses()
    {
        // All types should be either records, interfaces, exceptions, or static classes (for constants)
        var types = Types.InAssembly(CoreAssembly)
            .GetTypes();

        foreach (var type in types)
        {
            bool isInterface = type.IsInterface;
            bool isRecord = IsRecord(type);
            bool isException = typeof(Exception).IsAssignableFrom(type);
            bool isStaticClass = type.IsAbstract && type.IsSealed; // Static classes are abstract and sealed

            Assert.True(isInterface || isRecord || isException || isStaticClass,
                $"Type {type.FullName} in Core should be a record, interface, exception, or static class");
        }
    }

    [Fact]
    public void Application_ShouldOnlyReferenceCore()
    {
        // Application should only internally reference Core, not Infrastructure or Api
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, GetFailureMessage(result, "Application should only reference Core"));
    }

    [Fact]
    public void Application_ShouldNotHaveDirectIOImplementations()
    {
        // Application should not have direct I/O implementations
        // Note: System.Data.Common.DbException is allowed for exception handling
        var result = Types
            .InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("System.Net.Http", "System.IO.Pipelines", "Npgsql", "Dapper")
            .GetResult();

        Assert.True(result.IsSuccessful, GetFailureMessage(result, "Application should not have direct I/O dependencies"));
    }

    [Fact]
    public void Infrastructure_ShouldOnlyReferenceCore()
    {
        // Infrastructure should only internally reference Core, not Application or Api
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, GetFailureMessage(result, "Infrastructure should only reference Core"));
    }

    [Fact]
    public void Infrastructure_ShouldNotContainBusinessLogic()
    {
        // Infrastructure should not have business logic classes like Commands
        var typesEndingWithCommand = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("Command", StringComparison.Ordinal)
            .GetTypes();

        Assert.Empty(typesEndingWithCommand);
    }

    [Fact]
    public void Api_ShouldNotContainBusinessLogic()
    {
        // API should not have actual business logic like Commands
        var typesEndingWithCommand = Types
            .InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Command", StringComparison.Ordinal)
            .GetTypes();

        Assert.Empty(typesEndingWithCommand);
    }

    private static bool IsRecord(Type type)
    {
        // Records have a compiler-generated EqualityContract property
        return type.IsClass && type.GetMethod("<Clone>$") != null;
    }

    private static string GetFailureMessage(NetArchTest.Rules.TestResult result, string context)
    {
        if (result.IsSuccessful)
        {
            return string.Empty;
        }

        var failingTypes = result.FailingTypeNames ?? [];
        return $"{context}. Failing types: {string.Join(", ", failingTypes)}";
    }
}
