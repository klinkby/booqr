using System.Runtime.CompilerServices;

namespace Klinkby.Booqr.Application.Extensions;

internal static class EventExtensions
{
    private const long MarginTicks = TimeSpan.TicksPerMillisecond;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool StartIntersects<TEvent1, TEvent2>(this TEvent1 current, TEvent2 other)
        where TEvent1: notnull, IEvent
        where TEvent2: notnull, IEvent =>
        current.StartTime <= other.EndTime.AddTicks(MarginTicks) &&
        current.EndTime.AddTicks(MarginTicks) >= other.EndTime;

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static bool EndIntersects<TEvent1, TEvent2>(this TEvent1 current, TEvent2 other)
    //     where TEvent1 : notnull, IEvent
    //     where TEvent2 : notnull, IEvent =>
    //     current.EndTime >= other.StartTime.AddTicks(MarginTicks) &&
    //     current.EndTime <= other.EndTime.AddTicks(MarginTicks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<TEvent1, TEvent2>(this TEvent1 current, TEvent2 other)
        where TEvent1 : notnull, IEvent
        where TEvent2 : notnull, IEvent =>
        current.StartTime >= other.StartTime && current.StartTime <= other.EndTime || current.EndTime >= other.StartTime && current.EndTime <= other.EndTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompletelyWithin<TEvent1, TEvent2>(this TEvent1 current, TEvent2 other)
        where TEvent1: notnull, IEvent
        where TEvent2: notnull, IEvent =>
        current.StartTime >= other.StartTime.AddTicks(-MarginTicks) &&
        current.EndTime <= other.EndTime.AddTicks(MarginTicks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equalsish(this DateTime current, DateTime other) =>
        current <= other.AddTicks(MarginTicks) &&
        current.AddTicks(MarginTicks) >= other;}
