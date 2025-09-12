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
        int userId = query.AuthenticatedUserId;
        if (userId == query.Id)
        {
            return;
        }

        var isEmployee = query.User!.IsInRole(UserRole.Employee) || query.User.IsInRole(UserRole.Admin);
        if (isEmployee)
        {
            return;
        }

        LogCannotInspectBooking(logger, userId, query.Id);
        throw new UnauthorizedAccessException("Cannot list another customer's bookings");
    }

    [LoggerMessage(LogLevel.Warning,
        "User {UserId} is not permitted to inspect {Id}'s bookings")]
    private static partial void LogCannotInspectBooking(ILogger logger, int userId, int id);

}
