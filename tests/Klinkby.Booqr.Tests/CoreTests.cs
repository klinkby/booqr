namespace Klinkby.Booqr.Tests;

[Collection(nameof(ArchitectureTestFixture))]
public class CoreTests(ArchitectureTestFixture fixture)
{
    [Fact]
    public void Core_ShouldOnlyReferenceSystemAssemblies()
    {
        CoreTypes
            .Should()
            .NotDependOnAnyTypesThat()
            .DoNotResideInNamespaceMatching($"^(System\\.*|{Regex.Escape(ArchitectureTestFixture.Core)}|Coverlet\\.*)")
            .Because("Core should only reference BCL types")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void Core_ClassesShouldBeImmutable()
    {
        ArchRuleDefinition.Classes()
            .That()
            .Are(CoreTypes)
            .Should()
            .BeImmutable()
            .Because("Core classes should be immutable")
            .Check(fixture.Architecture);
    }
}
