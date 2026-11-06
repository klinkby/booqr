using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Models;

/// <summary>
///     The scope of a <see cref="TakeoutDocument" />.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TakeoutScope>))]
public enum TakeoutScope
{
    /// <summary>All active, non-historical records of the tenant (Admin only; importable).</summary>
    TenantWorkspace,

    /// <summary>A single customer's own data for GDPR portability (export only; never importable).</summary>
    CustomerGdpr
}

/// <summary>
///     Self-contained, versioned export/import envelope. Ids are export-local and only used to resolve references
///     within the document; they are never written to the database as-is.
/// </summary>
public sealed record TakeoutDocument(
    [property: Range(1, int.MaxValue)] int SchemaVersion,
    DateTime ExportedAt,
    [property: StringLength(63)] string TenantSlug,
    TakeoutScope Scope,
    IReadOnlyList<TakeoutUser> Users,
    IReadOnlyList<TakeoutLocation> Locations,
    IReadOnlyList<TakeoutService> Services,
    IReadOnlyList<TakeoutBooking> Bookings,
    IReadOnlyList<TakeoutCalendarSlot> Calendar)
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultSchemaUri = "/api/takeout-v1.schema.json";

    [JsonPropertyName("$schema")]
    [JsonPropertyOrder(-1)]
    public string Schema { get; init; } = DefaultSchemaUri;
}

/// <summary>A user. <see cref="Email" /> is omitted for employees in a customer's GDPR export.</summary>
public sealed record TakeoutUser(
    [property: Range(1, int.MaxValue)] int Id,
    [property: Required, AllowedValues(UserRole.Customer, UserRole.Employee, UserRole.Admin)]
    string Role,
    [property: EmailAddress, StringLength(255)]
    [property: JsonConverter(typeof(EmailJsonConverter))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Email = null,
    [property: StringLength(255)] string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? Phone = null);

public sealed record TakeoutLocation(
    [property: Range(1, int.MaxValue)] int Id,
    [property: Required, StringLength(255, MinimumLength = 1)]
    string Name,
    [property: StringLength(255)] string? Address1 = null,
    [property: StringLength(255)] string? Address2 = null,
    [property: StringLength(20)] string? Zip = null,
    [property: StringLength(255)] string? City = null);

public sealed record TakeoutService(
    [property: Range(1, int.MaxValue)] int Id,
    [property: Required, StringLength(255, MinimumLength = 1)]
    string Name,
    TimeSpan Duration,
    IReadOnlyList<int> EmployeeIds,
    [property: StringLength(2000)] string? Description = null);

public sealed record TakeoutBooking(
    [property: Range(1, int.MaxValue)] int Id,
    [property: Range(1, int.MaxValue)] int CustomerId,
    [property: Range(1, int.MaxValue)] int ServiceId,
    [property: StringLength(2000)] string? Notes = null);

public sealed record TakeoutCalendarSlot(
    [property: Range(1, int.MaxValue)] int Id,
    [property: Range(1, int.MaxValue)] int EmployeeId,
    [property: Range(1, int.MaxValue)] int LocationId,
    [property: Range(1, int.MaxValue)] int? BookingId,
    DateTime StartTime,
    DateTime EndTime) : IEvent;

[JsonConverter(typeof(JsonStringEnumConverter<TakeoutWarningCode>))]
public enum TakeoutWarningCode
{
    SchemaVersionMismatch,
    MatchedExistingByNaturalKey,
    UserCreatedWithoutPassword,
    MissingReferenceSkipped,
    CalendarOverlapSkipped,
    InvalidTimeRangeSkipped,
    HistoricalEntrySkipped,
    DuplicateSourceIdSkipped,
    UnmatchableUserSkipped
}

public sealed record TakeoutWarning(
    TakeoutWarningCode Code,
    string Entity,
    int? SourceId,
    string Message);

public sealed record EntityImportCounts(int Created, int Matched, int Skipped);

public sealed record ImportTakeoutResponse(
    bool ValidateOnly,
    EntityImportCounts Users,
    EntityImportCounts Locations,
    EntityImportCounts Services,
    EntityImportCounts Bookings,
    EntityImportCounts Calendar,
    IReadOnlyList<TakeoutWarning> Warnings);
