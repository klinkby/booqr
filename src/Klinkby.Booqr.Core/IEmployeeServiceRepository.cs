using System.Diagnostics.CodeAnalysis;

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
    ///     Assigns employees to a service.
    /// </summary>
    /// <param name="serviceId">The identifier of the service to assign employees to.</param>
    /// <param name="employeeIds">The identifiers of the employees to assign.</param>
    /// <param name="cancellation">A token to cancel the operation.</param>
    Task Assign(int serviceId, int[] employeeIds, CancellationToken cancellation = default);
}
