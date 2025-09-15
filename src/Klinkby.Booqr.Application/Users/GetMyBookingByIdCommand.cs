namespace Klinkby.Booqr.Application.Users;

public sealed record GetMyBookingByIdRequest(
    [Range(1, int.MaxValue)] int Id, // UserId that is
    [property: Range(1, int.MaxValue)] int BookingId
) : AuthenticatedRequest;

public sealed partial class GetMyBookingByIdCommand(
    IMyBookingRepository myBookingRepository,
    ILogger<GetMyBookingByIdCommand> logger) : ICommand<GetMyBookingByIdRequest, Task<MyBooking?>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<MyBooking?> Execute(GetMyBookingByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        ValidateUserAccess(query);
        var myBooking = await myBookingRepository.GetById(query.BookingId, cancellation);
        if (myBooking is null) return null;
        ValidateUserAccess(query, myBooking);

        return myBooking;
    }

    private void ValidateUserAccess(GetMyBookingByIdRequest query, MyBooking myBooking)
    {
        if (myBooking.CustomerId == query.Id || query.User!.IsInRole(UserRole.Employee) || query.User.IsInRole(UserRole.Admin))
        {
            return;
        }

        FailUnauthorized(query);
    }


    private void ValidateUserAccess(GetMyBookingByIdRequest query)
    {
        if (query.CanUserAccess(query.Id)) return;

        FailUnauthorized(query);
    }

    private void FailUnauthorized(GetMyBookingByIdRequest query)
    {
        _log.CannotInspectBooking(query.AuthenticatedUserId, query.Id);
        throw new UnauthorizedAccessException("Cannot list another customer's bookings");
    }


    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(200, LogLevel.Warning,
            "User {UserId} is not permitted to inspect {Id}'s booking")]
        public partial void CannotInspectBooking(int userId, int id);
    }
}
