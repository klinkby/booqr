using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Services;

public sealed record UpdateServiceRequest(
    [property: IgnoreDataMember] int Id,
    string Name,
    TimeSpan Duration,
    int[]? Employees,
    string? Description = null
    ) : AddServiceRequest(Name, Duration, Employees, Description), IId;

public sealed class UpdateServiceCommand(
    IServiceRepository services,
    IEmployeeServiceRepository employeeServiceRepository,
    ITransaction transaction,
    IRequestMetadata etagProvider,
    IActivityRecorder activityRecorder,
    ILogger<UpdateServiceCommand> logger)
    : UpdateCommand<UpdateServiceRequest, Service>(services, activityRecorder, logger)
{
    public override async Task<Result<bool>> Execute(UpdateServiceRequest query, CancellationToken cancellation = default)
    {
        await transaction.Begin(cancellation);
        try
        {
            Result<bool> result = await base.Execute(query, cancellation);
            if (!result.IsSuccess)
            {
                await transaction.Rollback(cancellation);
                return result;
            }

            if (query.Employees != null)
            {
                await employeeServiceRepository.Assign(query.Id, query.Employees, cancellation);
            }

            await transaction.Commit(cancellation);

            return result;
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }
    }

    protected override Service Map(UpdateServiceRequest query) =>
        new(query.Name, query.Duration, [], query.Description)
        {
            Id = query.Id,
            Version = etagProvider.Version
        };
}
