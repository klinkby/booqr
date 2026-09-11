namespace Klinkby.Booqr.Tests;

/// <summary>
///     Enforces that the elevated control-plane assembly (<c>Klinkby.Booqr.Control</c>) — which
///     holds the <c>booqr_migrator</c>/<c>booqr_batch</c> provisioning/migration logic — is never
///     reachable from the request-handling business or data layers. The Api assembly may reference
///     Control (it dispatches the <c>admin</c> flag before the web host starts), but Application and
///     Infrastructure must not, so provisioning code can never run on a request path.
/// </summary>
[Collection(nameof(ArchitectureTestFixture))]
public class ControlTests(ArchitectureTestFixture fixture)
{
    [Fact]
    public void Application_ShouldNotReferenceControl()
    {
        ApplicationTypes
            .Should()
            .NotDependOnAny(ControlTypes)
            .Because("Application (request-path business logic) must not reach control-plane provisioning")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void Infrastructure_ShouldNotReferenceControl()
    {
        InfrastructureTypes
            .Should()
            .NotDependOnAny(ControlTypes)
            .Because("Infrastructure (request-path data access) must not reach control-plane provisioning")
            .Check(fixture.Architecture);
    }

    [Fact]
    public void Control_ShouldNotReferenceApplication()
    {
        ControlTypes
            .Should()
            .NotDependOnAny(ApplicationTypes)
            .Because("Control is a leaf admin tool over Infrastructure/Core; it carries no business logic")
            .Check(fixture.Architecture);
    }
}
