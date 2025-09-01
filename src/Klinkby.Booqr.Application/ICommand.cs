namespace Klinkby.Booqr.Application;

public interface ICommand<in TQuery> where TQuery : notnull
{
    Task Execute(TQuery query, CancellationToken cancellation = default);
}

public interface ICommand<in TQuery, out TResponse> where TQuery : notnull
{
    TResponse Execute(TQuery query, CancellationToken cancellation = default);
}
