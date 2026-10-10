The string interpolation in Dapper SQL queries are required by DapperAOT interceptor:

```csharp
await connection.QuerySingleOrDefaultAsync<Booking>($"{GetByIdQuery}", new GetByIdParameters(id));
```

Row and parameter types passed to Dapper must be at least `internal`, never `private` nested types. The generated interceptor can't see private types, so it silently skips the call site. Dapper then falls back to reflection, and AOT publish fails with `IL2104`/`IL3053` on `Dapper.dll`.
