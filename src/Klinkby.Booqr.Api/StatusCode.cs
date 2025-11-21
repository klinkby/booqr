using System.Net.Sockets;
using Klinkby.Booqr.Core.Exceptions;
using Npgsql;

namespace Klinkby.Booqr.Api;

internal static class StatusCode
{
    internal static int FromException(Exception exception)
    {
        if (exception is AggregateException agg)
        {
            exception = agg.GetBaseException();
        }

        return exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            InvalidOperationException => StatusCodes.Status409Conflict,
            MidAirCollisionException => StatusCodes.Status412PreconditionFailed,
            SocketException => StatusCodes.Status502BadGateway,
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            NotImplementedException => StatusCodes.Status501NotImplemented,
            PostgresException pg => pg.ConstraintName is not null
                ? StatusCodes.Status409Conflict
                : pg.IsTransient
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status500InternalServerError,
            NpgsqlException pg => pg.IsTransient || pg.InnerException is IOException
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
