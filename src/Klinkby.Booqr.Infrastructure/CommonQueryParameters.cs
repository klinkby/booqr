namespace Klinkby.Booqr.Infrastructure;

internal record struct GetByIdParameters(int Id);

internal record struct UndeleteParameters(int Id);

internal record struct DeleteParameters(int Id, DateTimeOffset Now);
