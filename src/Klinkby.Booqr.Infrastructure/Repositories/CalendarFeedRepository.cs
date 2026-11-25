namespace Klinkby.Booqr.Infrastructure.Repositories;

internal sealed class CalendarFeedRepository(IConnectionProvider connectionProvider)
    : ICalendarFeedRepository
{
    public async IAsyncEnumerable<CalendarFeedEvent> GetEvents(int userId, DateTime fromTime, DateTime toTime, int maxEvents,
        [EnumeratorCancellation]
        CancellationToken cancellation = default)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<CalendarFeedEvent> query = connection.QueryUnbufferedAsync<CalendarFeedEvent>(
            $"""
             SELECT b.id, c.starttime, c.endtime, s.name AS service, l.name AS location,
                    l.address1, l.address2, l.zip, l.city,
                    GREATEST(b.modified, c.modified) AS modified
             FROM bookings b
             JOIN calendar c ON c.bookingid = b.id
             JOIN services s ON s.id = b.serviceid
             JOIN locations l ON l.id = c.locationid
             WHERE (b.customerid = @userId OR c.employeeid = @userId)
               AND c.starttime < @toTime
               AND c.endtime > @fromTime
               AND b.deleted IS NULL
               AND c.deleted IS NULL
             ORDER BY c.starttime, b.id
             LIMIT @maxEvents
             """, new { userId, fromTime, toTime, maxEvents });
        await foreach (CalendarFeedEvent item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }
}
