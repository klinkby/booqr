namespace Klinkby.Booqr.Tests;

[Collection(nameof(ArchitectureTestFixture))]
public class ApplicationTests(ArchitectureTestFixture fixture)
{
    [Fact]
    public void Application_ShouldNotReferenceInfrastructure()
    {
        ApplicationTypes
            .Should()
            .NotDependOnAny(InfrastructureTypes)
            .Because("Application should not depend on infrastructure implementation")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void Application_RequestClassesShouldBeImmutable()
    {
        RequestTypes
            .Should()
            .BeImmutable()
            .Because("Request objects should be immutable")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void Application_ShouldNotIO()
    {
        ApplicationTypes
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching("^(Dapper|System\\.Console|System\\.IO|System\\.Net|System\\.Data|Npgsql)")
            .Because("Application logic should not do IO")
            .Check(fixture.Architecture);
    }
}
