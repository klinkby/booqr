namespace Klinkby.Booqr.Core;

public sealed record MyBooking(
    DateTime StartTime,
    DateTime EndTime,
    int ServiceId,
    int LocationId,
    int EmployeeId,
    bool HasNotes) : Audit, IEvent;

public interface IMyBookingRepository : IRepository
{
    IAsyncEnumerable<MyBooking> GetRangeByUserId(int userId, DateTime fromTime, DateTime toTime, IPageQuery pageQuery,
        CancellationToken cancellation = default);
}
