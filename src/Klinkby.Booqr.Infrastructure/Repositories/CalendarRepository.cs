using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("employeeid", "starttime", "endtime", "locationid", "bookingid")]
internal sealed partial class CalendarRepository(IConnectionProvider connectionProvider, TimeProvider timeProvider)
    : AuditRepository<CalendarEvent>(timeProvider), ICalendarRepository
{
    private const string TableName = "calendar";

    public async IAsyncEnumerable<CalendarEvent> GetRange(
        DateTime fromTime, DateTime toTime,
        IPageQuery pageQuery,
        bool available, bool booked,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<CalendarEvent> query = connection.QueryUnbufferedAsync<CalendarEvent>(
            $"""
             SELECT id,{CommaSeparated},created,modified
             FROM {TableName}
             WHERE ( starttime BETWEEN @fromTime AND @toTime OR endtime BETWEEN @fromTime AND @toTime )
               AND bookingid IS NULL
               AND ((@available AND bookingid is NULL) OR (@booked AND bookingid IS NOT NULL))
               AND deleted IS NULL
             LIMIT @Num OFFSET @Start
             """, new { fromTime, toTime, pageQuery.Start, pageQuery.Num, available, booked });
        await foreach (CalendarEvent item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    public async Task<CalendarEvent?> GetByBookingId(int bookingId, CancellationToken cancellation = default)
    {

        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<CalendarEvent>(
            $"""
              SELECT id,{CommaSeparated},created,modified,deleted
              FROM {TableName}
              WHERE deleted IS NULL AND bookingid=@Id
              """, new GetByIdParameters(bookingId));
    }

    #region IRepository

    public async IAsyncEnumerable<CalendarEvent> GetAll(IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<CalendarEvent> query =
            connection.QueryUnbufferedAsync<CalendarEvent>($"{GetAllQuery}", pageQuery);
        await foreach (CalendarEvent item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    public async Task<CalendarEvent?> GetById(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<CalendarEvent>($"{GetByIdQuery}", new GetByIdParameters(id));
    }

    public async Task<int> Add(CalendarEvent newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        var result = await connection.ExecuteScalarAsync($"{InsertQuery}", WithCreated(newItem));
        Debug.Assert(result is int);
        return (int)result;
    }

    public async Task<bool> Update(CalendarEvent item, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{UpdateQuery}", WithModified(item));
    }

    public async Task<bool> Delete(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{DeleteQuery}", new DeleteParameters(id, Now));
    }

    public async Task<bool> Undelete(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync($"{UndeleteQuery}", new UndeleteParameters(id));
    }

    #endregion
}
