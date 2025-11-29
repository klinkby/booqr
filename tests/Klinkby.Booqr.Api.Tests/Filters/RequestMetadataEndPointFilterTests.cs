using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using AutoFixture.Xunit3;
using Klinkby.Booqr.Api.Filters;
using Klinkby.Booqr.Application.Abstractions;
using Klinkby.Booqr.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Klinkby.Booqr.Api.Tests.Filters;

public class RequestMetadataEndPointFilterTests
{
    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Fake")]
    public async Task GIVEN_NullFromNext_WHEN_InvokeAsync_THEN_Status404AndNullReturned()
    {
        var filter = new RequestMetadataEndPointFilter();
        (DefaultHttpContext httpContext, IServiceScope scope) = CreateHttpContext();
        await using IAsyncDisposable _ = scope as IAsyncDisposable ?? new DummyAsyncDisposable();

        httpContext.Request.Method = HttpMethods.Get;

        EndpointFilterInvocationContext ctx = new TestInvocationContext(httpContext);
        var result = await filter.InvokeAsync(ctx, _ => ValueTask.FromResult<object?>(null));

        Assert.Null(result);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
    }

    [Theory]
    [AutoData]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Fake")]
    public async Task GIVEN_GetWithIfNoneMatch_WHEN_InvokeAsync_THEN_VersionSetFromHeaderAndTraceIdCopied(
        DateTime version,
        string traceId,
        object nextResponse)
    {
        var filter = new RequestMetadataEndPointFilter();
        (DefaultHttpContext httpContext, IServiceScope scope) = CreateHttpContext();
        await using IAsyncDisposable _ = scope as IAsyncDisposable ?? new DummyAsyncDisposable();

        var versionUtc = DateTime.SpecifyKind(version, DateTimeKind.Utc);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Headers.IfNoneMatch = TicksOf(versionUtc);
        httpContext.TraceIdentifier = traceId;

        EndpointFilterInvocationContext ctx = new TestInvocationContext(httpContext);
        var result = await filter.InvokeAsync(ctx, _ => new ValueTask<object?>(nextResponse));

        Assert.Same(nextResponse, result);

        IRequestMetadata meta = httpContext.RequestServices.GetRequiredService<IRequestMetadata>();
        Assert.Equal(versionUtc, meta.Version);
        Assert.Equal(httpContext.TraceIdentifier, meta.TraceId);
    }

    [Theory]
    [AutoData]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Fake")]
    public async Task GIVEN_PutWithIfMatch_WHEN_InvokeAsync_THEN_VersionSetFromHeader(
        DateTime version,
        object nextResponse)
    {
        var filter = new RequestMetadataEndPointFilter();
        (DefaultHttpContext httpContext, IServiceScope scope) = CreateHttpContext();
        await using IAsyncDisposable _ = scope as IAsyncDisposable ?? new DummyAsyncDisposable();

        var versionUtc = DateTime.SpecifyKind(version, DateTimeKind.Utc);
        httpContext.Request.Method = HttpMethods.Put;
        httpContext.Request.Headers.IfMatch = TicksOf(versionUtc);

        EndpointFilterInvocationContext ctx = new TestInvocationContext(httpContext);
        var result = await filter.InvokeAsync(ctx, _ => new ValueTask<object?>(nextResponse));

        Assert.Same(nextResponse, result);

        IRequestMetadata meta = httpContext.RequestServices.GetRequiredService<IRequestMetadata>();
        Assert.Equal(versionUtc, meta.Version);
    }

    [Theory]
    [AutoData]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Fake")]
    public async Task GIVEN_AuditWithMatchingVersion_WHEN_InvokeAsync_THEN_Status304AndNullReturned(
        int id,
        DateTime version)
    {
        var filter = new RequestMetadataEndPointFilter();
        (DefaultHttpContext httpContext, IServiceScope scope) = CreateHttpContext();
        await using IAsyncDisposable _ = scope as IAsyncDisposable ?? new DummyAsyncDisposable();

        var versionUtc = DateTime.SpecifyKind(version, DateTimeKind.Utc);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Headers.IfNoneMatch = TicksOf(versionUtc);

        var audit = new TestAudit(id, versionUtc, versionUtc);

        EndpointFilterInvocationContext ctx = new TestInvocationContext(httpContext);
        var result = await filter.InvokeAsync(ctx, _ => new ValueTask<object?>(audit));

        Assert.Null(result);
        Assert.Equal(StatusCodes.Status304NotModified, httpContext.Response.StatusCode);
        // Ensure no ETag is appended on 304 path
        Assert.False(httpContext.Response.Headers.ContainsKey("ETag"));
    }

    [Theory]
    [AutoData]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Fake")]
    public async Task GIVEN_AuditWithoutMatchingVersion_WHEN_InvokeAsync_THEN_ETagHeaderAddedAndResponseReturned(
        int id,
        DateTime version)
    {
        var filter = new RequestMetadataEndPointFilter();
        (DefaultHttpContext httpContext, IServiceScope scope) = CreateHttpContext();
        await using IAsyncDisposable _ = scope as IAsyncDisposable ?? new DummyAsyncDisposable();

        var versionUtc = DateTime.SpecifyKind(version, DateTimeKind.Utc);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Headers.IfNoneMatch = TicksOf(versionUtc);

        DateTime modified = versionUtc.AddMinutes(1);
        var audit = new TestAudit(id, versionUtc, modified);

        EndpointFilterInvocationContext ctx = new TestInvocationContext(httpContext);
        var result = await filter.InvokeAsync(ctx, _ => new ValueTask<object?>(audit));

        Assert.Same(audit, result);
        Assert.True(httpContext.Response.Headers.TryGetValue("ETag", out StringValues etag));
        Assert.Equal(TicksOf(modified), etag.ToString());
    }

    private sealed class TestInvocationContext(HttpContext httpContext) : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = httpContext;

        public override IList<object?> Arguments => Array.Empty<object?>();

        public override T GetArgument<T>(int index)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private sealed record TestAudit : Audit
    {
        public TestAudit(int id, DateTime created, DateTime modified)
        {
            Id = id;
            Created = created;
            Modified = modified;
        }
    }

    private sealed class DummyAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private static (DefaultHttpContext HttpContext, IServiceScope Scope) CreateHttpContext()
    {
        var services = new ServiceCollection();
        // Register the concrete internal RequestMetadata type via reflection, so the filter's cast works
        Assembly apiAssembly = typeof(RequestMetadataEndPointFilter).Assembly;
        Type requestMetadataType = apiAssembly.GetType("Klinkby.Booqr.Api.Models.RequestMetadata", true)!;
        services.AddScoped(typeof(IRequestMetadata),
            _ => (IRequestMetadata)Activator.CreateInstance(requestMetadataType)!);

        ServiceProvider provider = services.BuildServiceProvider();
        IServiceScope scope = provider.CreateScope();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };

        // Minimal endpoint is required by some framework features, but not strictly used here
        httpContext.SetEndpoint(
            new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "test-endpoint"));
        return (httpContext, scope);
    }

    private static string TicksOf(DateTime dt)
    {
        return dt.Ticks.ToString(CultureInfo.InvariantCulture);
    }
}
