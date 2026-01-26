The string interpolation in Dapper SQL queries are required by DapperAOT interceptor:

```csharp
await connection.QuerySingleOrDefaultAsync<Booking>($"{GetByIdQuery}", new GetByIdParameters(id));
```
