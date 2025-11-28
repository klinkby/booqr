using Klinkby.Booqr.Core;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Klinkby.Booqr.Tests;

[Collection(nameof(ArchitectureTestFixture))]
public class InfrastructureTests(ArchitectureTestFixture fixture)
{
    [Fact]
    public void Infrastructure_ShouldNotReferenceApplication()
    {
        InfrastructureTypes
            .Should()
            .NotDependOnAny(ApplicationTypes)
            .Because("Infrastructure should not depend on application logic")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void InfrastructureRepositories_ShouldBeSealed()
    {
        Classes()
            .That()
            .Are(InfrastructureTypes)
            .And()
            .ImplementInterface(typeof(IRepository))
            .Should()
            .BeSealed()
            .Because("Repositories should be sealed")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void Repositories_AreInfrastructure()
    {
        Classes()
            .That()
            .ImplementInterface(typeof(IRepository))
            .Should()
            .ResideInAssemblyMatching(Regex.Escape(ArchitectureTestFixture.Infrastructure))
            .Because("Repositories are infrastructure")
            .Check(fixture.Architecture);
    }
}
