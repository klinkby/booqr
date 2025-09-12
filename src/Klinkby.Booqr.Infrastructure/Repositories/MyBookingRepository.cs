using System.Runtime.CompilerServices;

namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("starttime", "endtime", "customerid", "serviceid", "locationid", "employeeid", "hasnote")]
internal sealed partial class MyBookingRepository(IConnectionProvider connectionProvider, TimeProvider timeProvider)
    : AuditRepository<MyBooking>(timeProvider), IMyBookingRepository
{
    private const string TableName = "mybookings";

    public async IAsyncEnumerable<MyBooking> GetRangeByUserId(int userId, DateTime fromTime, DateTime toTime,
        IPageQuery pageQuery,
        [EnumeratorCancellation]
        CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<MyBooking> query = connection.QueryUnbufferedAsync<MyBooking>(
            $"""
             SELECT id,{CommaSeparated},created,modified
             FROM {TableName}
             WHERE ( starttime BETWEEN @fromTime AND @toTime OR endtime BETWEEN @fromTime AND @toTime )
               AND ( customerid=@userId OR employeeid=@userId )
               AND deleted IS NULL
             ORDER BY starttime
             LIMIT @Num OFFSET @Start
             """, new { fromTime, toTime, pageQuery.Start, pageQuery.Num, userId });
        await foreach (MyBooking item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }
}
