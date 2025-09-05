using Klinkby.Booqr.Application.Calendar;
using Klinkby.Booqr.Application.Vacancies;

namespace Klinkby.Booqr.Api;

internal static partial class Routes
{
    private static void MapVacancies(IEndpointRouteBuilder app)
    {
        const string resourceName = "vacancies";

        RouteGroupBuilder group = app
            .MapGroup(resourceName)
            .WithTags("Vacancy");

        group.MapGet("",
                static (GetVacancyCollectionCommand command,
                        [AsParameters] GetVacanciesRequest request,
                        CancellationToken cancellation) =>
                    command.GetCollection(request, cancellation))
            .WithSummary("List vacancies");

        group.MapGet("{id}",
                static (GetVacancyByIdCommand byIdCommand, [AsParameters] ByIdRequest request,
                        CancellationToken cancellation) =>
                    byIdCommand.GetSingle(request, cancellation))
            .WithSummary("Get a single vacancy");

        group.MapPost("",
                static (AddVacancyCommand command,
                        [FromBody] AddVacancyRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.Created(request, user, $"{BaseUrl}/{resourceName}", cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithSummary("Add a vacancy");

        // group.MapPut("{id}",
        //         static (UpdateVacancyCommand command,
        //                 int id,
        //                 [FromBody] UpdateVacancyRequest request,
        //                 ClaimsPrincipal user, CancellationToken cancellation) =>
        //             command.NoContent(request with { Id = id }, user, cancellation))
        //     .RequireAuthorization(UserRole.Employee)
        //     .WithSummary("Update a vacancy");
        //
        group.MapDelete("{id}",
                static (DeleteVacancyCommand command,
                        [AsParameters] AuthenticatedByIdRequest request,
                        ClaimsPrincipal user, CancellationToken cancellation) =>
                    command.NoContent(request, user, cancellation))
            .RequireAuthorization(UserRole.Employee)
            .WithSummary("Delete a vacancy");
    }
}
