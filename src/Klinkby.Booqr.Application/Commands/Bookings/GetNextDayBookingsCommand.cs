using System.Diagnostics.CodeAnalysis;
namespace Klinkby.Booqr.Application.Commands.Bookings;

public sealed record GetBookingDetailsRequest(
    [property: Required] DateOnly Date);

public sealed partial class GetBookingDetailsCommand(
    IBookingDetailsRepository bookingsDetails,
    ILogger<GetBookingDetailsCommand> logger
) : ICommand<GetBookingDetailsRequest, IAsyncEnumerable<BookingDetails>>
{
    private readonly LoggerMessages _logger = new(logger);

    public IAsyncEnumerable<BookingDetails> Execute(GetBookingDetailsRequest query,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var fromTime = query.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime toTime = fromTime + TimeSpan.FromDays(1);

        _logger.GetBookingDetails(fromTime, toTime);

        return bookingsDetails.GetRange(fromTime, toTime, new PageQuery(), cancellation);
    }

    [ExcludeFromCodeCoverage]
    private sealed partial class LoggerMessages(ILogger logger)
    {
        [LoggerMessage(220, LogLevel.Information, "Get bookings from {FromTime} to {ToTime}")]
        public partial void GetBookingDetails(DateTime fromTime, DateTime toTime);
    }
}
