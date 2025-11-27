using System.Text.Json.Serialization;
using Klinkby.Booqr.Application.Models;

namespace Klinkby.Booqr.Api.Util;

[JsonSerializable(typeof(AddBookingRequest))]
[JsonSerializable(typeof(AddLocationRequest))]
[JsonSerializable(typeof(AddServiceRequest))]
[JsonSerializable(typeof(AddVacancyRequest))]
[JsonSerializable(typeof(AuthenticatedByIdRequest))]
[JsonSerializable(typeof(Booking))]
[JsonSerializable(typeof(ByIdRequest))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(CollectionResponse<Booking>))]
[JsonSerializable(typeof(CollectionResponse<CalendarEvent>))]
[JsonSerializable(typeof(CollectionResponse<Location>))]
[JsonSerializable(typeof(CollectionResponse<MyBooking>))]
[JsonSerializable(typeof(CollectionResponse<Service>))]
[JsonSerializable(typeof(CollectionResponse<User>))]
[JsonSerializable(typeof(CreatedResponse))]
[JsonSerializable(typeof(GetMyBookingsRequest))]
[JsonSerializable(typeof(GetVacanciesRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(PageQuery))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(ResetPasswordRequest))]
[JsonSerializable(typeof(SignUpRequest))]
[JsonSerializable(typeof(UpdateLocationRequest))]
[JsonSerializable(typeof(UpdateServiceRequest))]
[JsonSerializable(typeof(UpdateUserProfileRequest))]
[JsonSerializable(typeof(User))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext;
