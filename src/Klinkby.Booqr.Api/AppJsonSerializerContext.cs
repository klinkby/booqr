using System.Text.Json.Serialization;
using Klinkby.Booqr.Application.Calendar;
using Klinkby.Booqr.Application.Locations;
using Klinkby.Booqr.Application.Services;
using Klinkby.Booqr.Application.Users;

namespace Klinkby.Booqr.Api;

[JsonSerializable(typeof(PageQuery))]
[JsonSerializable(typeof(ByIdRequest))]
[JsonSerializable(typeof(AuthenticatedByIdRequest))]
[JsonSerializable(typeof(CreatedResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(GetAvailableEventsRequest))]
[JsonSerializable(typeof(CollectionResponse<CalendarEvent>))]
[JsonSerializable(typeof(AddServiceRequest))]
[JsonSerializable(typeof(UpdateServiceRequest))]
[JsonSerializable(typeof(CollectionResponse<Service>))]
[JsonSerializable(typeof(PageQuery))]
[JsonSerializable(typeof(AddLocationRequest))]
[JsonSerializable(typeof(UpdateLocationRequest))]
[JsonSerializable(typeof(CollectionResponse<Location>))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
