using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application;

/// <summary>
///     C# 15 migration: delete the abstract record + ctor, hoist the two case
///     records to top level, declare `public union Result&lt;T&gt;(Success&lt;T&gt;,
///     Error);` — switch arms stay, the `_` arms get deleted.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Result<>.Success), "success")]
[JsonDerivedType(typeof(Result<>.Fault), "fault")]
[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")]
[SuppressMessage("Design", "CA1034:Nested types should not be visible")]
public abstract record Result<T> where T : notnull
{
    // hierarchy is effectively closed today
    private protected Result()
    {
    }

    public bool IsSuccess => this is Success;

    public T? ValueOrDefault(T? fallback = default) => this is Success s ? s.Value : fallback;

    // Ergonomics: return values/errors without `new Result<User>.Success(u)` noise.
    public static implicit operator Result<T>(T value) => new Success(value);

    public static implicit operator Result<T>(Problem problem) => new Fault(problem);

    public sealed record Success(T Value) : Result<T>;

    public sealed record Fault(Problem Problem) : Result<T>;
}
