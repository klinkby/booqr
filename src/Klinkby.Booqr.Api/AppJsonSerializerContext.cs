using System.Text.Json.Serialization;
using Klinkby.Booqr.Application.Bookings;
using Klinkby.Booqr.Application.Calendar;
using Klinkby.Booqr.Application.Locations;
using Klinkby.Booqr.Application.Services;
using Klinkby.Booqr.Application.Users;
using Klinkby.Booqr.Application.Vacancies;

namespace Klinkby.Booqr.Api;

[JsonSerializable(typeof(AddBookingRequest))]
[JsonSerializable(typeof(AddLocationRequest))]
[JsonSerializable(typeof(AddServiceRequest))]
[JsonSerializable(typeof(AddVacancyRequest))]
[JsonSerializable(typeof(AuthenticatedByIdRequest))]
[JsonSerializable(typeof(ByIdRequest))]
[JsonSerializable(typeof(CollectionResponse<Booking>))]
[JsonSerializable(typeof(CollectionResponse<CalendarEvent>))]
[JsonSerializable(typeof(CollectionResponse<Location>))]
[JsonSerializable(typeof(CollectionResponse<Service>))]
[JsonSerializable(typeof(CreatedResponse))]
[JsonSerializable(typeof(GetVacanciesRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(PageQuery))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(UpdateLocationRequest))]
[JsonSerializable(typeof(UpdateServiceRequest))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
