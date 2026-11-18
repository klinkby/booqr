using System.Text.Json;
using AutoFixture.Xunit3;
using Klinkby.Booqr.Api.Models;
using Klinkby.Booqr.Api.Util;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Api.Tests;

/// <summary>
///     Guards the wire contract of <see cref="MyBooking" />: the property is named <c>HasNote</c> to match the
///     <c>mybookings.hasnote</c> column, but must still serialize as <c>hasNotes</c> via the source-gen context.
/// </summary>
public class MyBookingSerializationTests
{
    private static readonly JsonSerializerOptions WebOptions =
        new(JsonSerializerDefaults.Web) { TypeInfoResolver = AppJsonSerializerContext.Default };

    [Theory]
    [AutoData]
    public void GIVEN_MyBooking_WHEN_Serialize_THEN_HasNotesPropertyKept(MyBooking booking)
    {
        using JsonDocument json =
            JsonDocument.Parse(JsonSerializer.Serialize(new CollectionResponse<MyBooking>([booking]), WebOptions));

        JsonElement item = json.RootElement.GetProperty("items")[0];
        Assert.Equal(booking.HasNote, item.GetProperty("hasNotes").GetBoolean());
        Assert.False(item.TryGetProperty("hasNote", out _));
    }
}
