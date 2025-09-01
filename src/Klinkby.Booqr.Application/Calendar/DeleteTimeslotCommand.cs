namespace Klinkby.Booqr.Application.Calendar;

public sealed class DeleteEventCommand(
    // ILogger<DeleteEventCommand> logger
    ) : ICommand<AuthenticatedByIdRequest>
{
    public Task Execute(AuthenticatedByIdRequest query, CancellationToken cancellation = default)
    {
        return Task.CompletedTask;
    }
}
