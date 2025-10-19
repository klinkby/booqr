using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Klinkby.Booqr.Infrastructure.Repositories;

[QueryFields("customerid", "serviceid", "notes")]
internal sealed partial class BookingRepository(IConnectionProvider connectionProvider, TimeProvider timeProvider)
    : AuditRepository<Booking>(timeProvider), IBookingRepository
{
    private const string TableName = "bookings";

    #region IRepository

    public async IAsyncEnumerable<Booking> GetAll(IPageQuery pageQuery,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        IAsyncEnumerable<Booking> query =
            connection.QueryUnbufferedAsync<Booking>(GetAllQuery, pageQuery);
        await foreach (Booking item in query.WithCancellation(cancellation))
        {
            yield return item;
        }
    }

    public async Task<Booking?> GetById(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return await connection.QuerySingleOrDefaultAsync<Booking>(GetByIdQuery, new GetByIdParameters(id));
    }

    public async Task<int> Add(Booking newItem, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        var result = await connection.ExecuteScalarAsync(InsertQuery, WithCreated(newItem));
        Debug.Assert(result is int);
        return (int)result;
    }

    public async Task<bool> Update(Booking item, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync(UpdateQuery, WithModified(item));
    }

    public async Task<bool> Delete(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync(DeleteQuery, new DeleteParameters(id, Now));
    }

    public async Task<bool> Undelete(int id, CancellationToken cancellation)
    {
        DbConnection connection = await connectionProvider.GetConnection(cancellation);
        return 1 == await connection.ExecuteAsync(UndeleteQuery, new UndeleteParameters(id));
    }

    #endregion
}
