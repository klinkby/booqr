using System.Diagnostics.CodeAnalysis;
namespace Klinkby.Booqr.Application.Commands.Users;

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
    ILogger<GetMyBookingsCommand> logger) : ICommand<GetMyBookingsRequest, Task<Result<List<MyBooking>>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<List<MyBooking>>> Execute(GetMyBookingsRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.IsStaffOrOwner(query.Id))
        {
            _log.CannotInspectBooking(query.AuthenticatedUserId, query.Id);
            return Problem.Forbidden;
        }

        List<MyBooking> bookings = await myBookingRepository.GetRangeByUserId(
                query.Id,
                query.FromTime ?? timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
                query.ToTime ?? DateTime.MaxValue,
                query,
                cancellation)
            .ToListAsync(cancellation);

        return bookings;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(120, LogLevel.Warning,
            "User {UserId} is not permitted to inspect {Id}'s bookings")]
        public partial void CannotInspectBooking(int userId, int id);
    }
}
