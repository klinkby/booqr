using System.Diagnostics.CodeAnalysis;

namespace Klinkby.Booqr.Application;

public sealed record Success<T>(T Value) where T : notnull;

public sealed record Fault(Problem Problem);

/// <summary>
///     Closed outcome of a command: either a <see cref="Success{T}" /> or a <see cref="Fault" />.
///     Being a C# union, <c>switch</c>es over it are exhaustive without a discard arm.
/// </summary>
[SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")]
public union Result<T>(Success<T>, Fault) : IEquatable<Result<T>> where T : notnull
{
    // `Value is …` rather than `this is …`: CA1508 misreads union patterns as always-true.
    public bool IsSuccess => Value is Success<T>;

    public T? ValueOrDefault(T? fallback = default) => Value is Success<T> s ? s.Value : fallback;

    // Ergonomics: return values/errors without `new Success<User>(u)` noise.
    public static implicit operator Result<T>(T value) => new Success<T>(value);

    public static implicit operator Result<T>(Problem problem) => new Fault(problem);

    // Keep the value equality the former record hierarchy had (and avoid reflective ValueType.Equals under AOT).
    public bool Equals(Result<T> other) => Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}
