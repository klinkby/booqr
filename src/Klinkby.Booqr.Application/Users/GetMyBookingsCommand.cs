namespace Klinkby.Booqr.Application.Users;

public sealed record GetMyBookingsRequest(
    [property: Range(1, int.MaxValue)] int Id, // UserId that is
    DateTime? FromTime,
    DateTime? ToTime,
    [Range(0, int.MaxValue)] int? Start = 0,
    [Range(1, 1000)] int? Num = 100)
    : AuthenticatedRequest, IPageQuery;

public sealed partial class GetMyBookingsCommand(
    IMyBookingRepository myBookingRepository,
    TimeProvider timeProvider,
    ILogger<GetMyBookingsCommand> logger) : ICommand<GetMyBookingsRequest, IAsyncEnumerable<MyBooking>>
{
    private readonly LoggerMessages _log = new(logger);

    public IAsyncEnumerable<MyBooking> Execute(GetMyBookingsRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateUserAccess(query);

        return myBookingRepository.GetRangeByUserId(
            query.Id,
            query.FromTime ?? timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
            query.ToTime ?? DateTime.MaxValue,
            query,
            cancellation);
    }

    private void ValidateUserAccess(GetMyBookingsRequest query)
    {
        var userId = query.AuthenticatedUserId;
        if (userId == query.Id)
        {
            return;
        }

        var isEmployee = query.User!.IsInRole(UserRole.Employee) || query.User.IsInRole(UserRole.Admin);
        if (isEmployee)
        {
            return;
        }

        _log.CannotInspectBooking(userId, query.Id);
        throw new UnauthorizedAccessException("Cannot list another customer's bookings");
    }


    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(120, LogLevel.Warning,
            "User {UserId} is not permitted to inspect {Id}'s bookings")]
        public partial void CannotInspectBooking(int userId, int id);
    }
}
