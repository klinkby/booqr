namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("id, starttime", "service", "duration", "location", "employee", "customerid", "customername", "customeremail")]
internal sealed partial class BookingDetailsRepository(
    IConnectionProvider connectionProvider)
    : IBookingDetailsRepository
{
    private const string TableName = "bookingdetails";

    public async IAsyncEnumerable<BookingDetails> GetRange(DateTime fromTime, DateTime toTime,
        IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<BookingDetails> query = connection.QueryUnbufferedAsync<BookingDetails>(
            $"""
             SELECT id,{CommaSeparated},created,modified
             FROM {TableName}
             WHERE ( starttime BETWEEN @fromTime AND @toTime )
               AND deleted IS NULL
             ORDER BY starttime
             LIMIT @Num OFFSET @Start
             """, new { fromTime, toTime, pageQuery.Start, pageQuery.Num });
        await foreach (BookingDetails item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }
}
