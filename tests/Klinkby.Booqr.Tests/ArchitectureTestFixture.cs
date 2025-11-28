using System.Diagnostics.CodeAnalysis;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Klinkby.Booqr.Tests;

/// <summary>
///     Architecture tests to enforce structural policies as defined in ARCHITECTURE.md
/// </summary>
public sealed class ArchitectureTestFixture
{
    private const string RootNamespace = "Klinkby.Booqr";

    internal const string Core = $"{RootNamespace}.{nameof(Core)}";
    internal const string Application = $"{RootNamespace}.{nameof(Booqr.Application)}";
    internal const string Infrastructure = $"{RootNamespace}.{nameof(Booqr.Infrastructure)}";
    internal const string Api = $"{RootNamespace}.{nameof(Booqr.Api)}";

    internal Architecture Architecture { get; } = new ArchLoader()
        .LoadAssemblies(
            Assembly.Load(Core),
            Assembly.Load(Application),
            Assembly.Load(Infrastructure),
            Assembly.Load(Api))
        .Build();

    internal static GivenTypesConjunctionWithDescription CoreTypes => Types()
        .That()
        .ResideInAssemblyMatching(Regex.Escape(Core))
        .As("Core types");

    internal static GivenTypesConjunctionWithDescription ApplicationTypes => Types()
        .That()
        .ResideInAssemblyMatching(Regex.Escape(Application))
        .And()
        .DoNotHaveNameMatching("EmbeddedResource")
        .As("Application types");

    internal static GivenTypesConjunctionWithDescription InfrastructureTypes => Types()
        .That()
        .ResideInAssemblyMatching(Regex.Escape(Infrastructure))
        .As("Infrastructure types");

    internal static GivenClassesConjunctionWithDescription RequestTypes => Classes()
        .That()
        .Are(ApplicationTypes)
        .And()
        .HaveNameMatching("Request$")
        .As("Requst types");
}

[CollectionDefinition(nameof(ArchitectureTestFixture))]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
public class ArchitectureTestCollectionFixture : ICollectionFixture<ArchitectureTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
