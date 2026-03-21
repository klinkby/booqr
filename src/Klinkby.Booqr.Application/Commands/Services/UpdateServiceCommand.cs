using System.Runtime.Serialization;

namespace Klinkby.Booqr.Application.Commands.Services;

public sealed record UpdateServiceRequest(
    [property: IgnoreDataMember] int Id,
    string Name,
    TimeSpan Duration,
    int[]? Employees
    ) : AddServiceRequest(Name, Duration, Employees), IId;

public sealed class UpdateServiceCommand(
    IServiceRepository services,
    IEmployeeServiceRepository employeeServiceRepository,
    ITransaction transaction,
    IRequestMetadata etagProvider,
    IActivityRecorder activityRecorder,
    ILogger<UpdateServiceCommand> logger)
    : UpdateCommand<UpdateServiceRequest, Service>(services, activityRecorder, logger)
{
    public override async Task Execute(UpdateServiceRequest query, CancellationToken cancellation = default)
    {
        await transaction.Begin(cancellation);
        try
        {
            await base.Execute(query, cancellation);
            if (query.Employees != null)
            {
                await employeeServiceRepository.Assign(query.Id, query.Employees, cancellation);
            }
            await transaction.Commit(cancellation);
        }
        catch
        {
            await transaction.Rollback(cancellation);
            throw;
        }
    }

    protected override Service Map(UpdateServiceRequest query) =>
        new(query.Name, query.Duration, [])
        {
            Id = query.Id,
            Version = etagProvider.Version
        };
}
