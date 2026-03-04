namespace Klinkby.Booqr.Core;

/// <summary>
///     Represents the assignment of a bookable service to an employee.
/// </summary>
/// <param name="EmployeeId">The identifier of the employee.</param>
/// <param name="ServiceId">The identifier of the service the employee can provide.</param>
public sealed record EmployeeService(int EmployeeId, int ServiceId)
{
    public static int CompositeId(int employeeId, int serviceId) => HashCode.Combine(employeeId, serviceId);
}

/// <summary>
///     Provides data access operations for the employee-service assignments.
/// </summary>
public interface IEmployeeServiceRepository : IRepository
{
    /// <summary>
    ///     Retrieves all services assigned to a specific employee.
    /// </summary>
    /// <param name="employeeId">The identifier of the employee.</param>
    /// <param name="pageQuery">The pagination parameters.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream of <see cref="Service"/> instances.</returns>
    IAsyncEnumerable<Service> GetByEmployeeId(int employeeId, IPageQuery pageQuery,
        CancellationToken cancellation = default);

    /// <summary>
    ///     Assigns a service to an employee.
    /// </summary>
    /// <param name="employeeId">The identifier of the employee.</param>
    /// <param name="serviceId">The identifier of the service to assign.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    Task Add(int employeeId, int serviceId, CancellationToken cancellation = default);

    /// <summary>
    ///     Removes a service assignment from an employee.
    /// </summary>
    /// <param name="employeeId">The identifier of the employee.</param>
    /// <param name="serviceId">The identifier of the service to remove.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    /// <returns><c>true</c> if the assignment was removed; otherwise <c>false</c>.</returns>
    Task<bool> Delete(int employeeId, int serviceId, CancellationToken cancellation = default);
}
