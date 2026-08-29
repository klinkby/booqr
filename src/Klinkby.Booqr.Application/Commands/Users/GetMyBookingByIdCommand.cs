using System.Diagnostics.CodeAnalysis;
namespace Klinkby.Booqr.Application.Commands.Users;

public sealed record GetMyBookingByIdRequest(
    [Range(1, int.MaxValue)] int Id, // UserId that is
    [property: Range(1, int.MaxValue)] int BookingId
) : AuthenticatedRequest;

public sealed partial class GetMyBookingByIdCommand(
    IMyBookingRepository myBookingRepository,
    ILogger<GetMyBookingByIdCommand> logger) : ICommand<GetMyBookingByIdRequest, Task<Result<MyBooking>>>
{
    private readonly LoggerMessages _log = new(logger);

    public async Task<Result<MyBooking>> Execute(GetMyBookingByIdRequest query, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!query.IsStaffOrOwner(query.Id))
        {
            _log.CannotInspectBooking(query.AuthenticatedUserId, query.Id);
            return Problem.Forbidden with { Detail = "You cannot inspect another customer's booking" };
        }

        MyBooking? myBooking = await myBookingRepository.GetById(query.BookingId, cancellation);

        if (myBooking is null)
            return Problem.NotFound with { Detail = $"Booking {query.BookingId} was not found"};

        if (!query.IsStaffOrOwner(myBooking.CustomerId))
        {
            _log.CannotInspectBooking(query.AuthenticatedUserId, myBooking.CustomerId);
            return Problem.Forbidden with { Detail = "You cannot inspect another customer's booking" };
        }

        return myBooking;
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(200, LogLevel.Warning,
            "User {UserId} is not permitted to inspect {Id}'s booking")]
        public partial void CannotInspectBooking(int userId, int id);
    }
}
