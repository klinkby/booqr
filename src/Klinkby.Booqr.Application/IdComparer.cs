namespace Klinkby.Booqr.Application;

internal sealed class IdComparer<T> : IEqualityComparer<T>
    where T: notnull, IId
{
    internal readonly static IdComparer<T> Instance = new();

    public bool Equals(T? x, T? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null)
        {
            return false;
        }

        if (y is null)
        {
            return false;
        }

        if (x.GetType() != y.GetType())
        {
            return false;
        }

        return x.Id == y.Id;
    }

    public int GetHashCode(T obj)
    {
        return obj.Id;
    }
}
