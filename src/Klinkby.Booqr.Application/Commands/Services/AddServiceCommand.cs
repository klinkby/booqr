using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application.Commands.Services;

public record AddServiceRequest(
    [property: Required]
    [property: StringLength(0xff)]
    string Name,
    TimeSpan Duration,
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Performance")]
    int[]? Employees
) : AuthenticatedRequest;

public sealed class AddServiceCommand(
    IServiceRepository services,
    IEmployeeServiceRepository employeeServiceRepository,
    ITransaction transaction,
    IActivityRecorder activityRecorder,
    ILogger<AddServiceCommand> logger)
    : AddCommand<AddServiceRequest, Service>(services, activityRecorder, logger)
{
    public override async Task<Result<int>> Execute(AddServiceRequest query, CancellationToken cancellation = default)
    {
        await transaction.Begin(cancellation);
        try
        {
            Result<int> result = await base.Execute(query, cancellation);
            if (result is not Result<int>.Success success)
            {
                await transaction.Rollback(cancellation);
                return result;
            }

            if (query.Employees != null)
            {
                await employeeServiceRepository.Assign(success.Value, query.Employees, cancellation);
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

    protected override Service Map(AddServiceRequest query) =>
        new(query.Name, query.Duration, []);
}
