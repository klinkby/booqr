// using Klinkby.Booqr.Core;
// using Microsoft.Extensions.DependencyInjection;
//
// namespace Klinkby.Booqr.Infrastructure.Tests;
//
// public sealed class TenantsRepositoryTests : PostgreSqlFixture
// {
//     [Theory]
//     [InlineData("postgres")]
//     public async Task GIVEN_Ok_THEN_Get_Succeeds(string dbName)
//     {
//         Tenant expected = new(dbName);
//         PageQuery pageQuery = new(0, 10);
//         var sut = Services.GetRequiredService<ITenantRepository>();
//         var actual = await sut
//             .GetAll(pageQuery, CancellationToken.None)
//             .ToListAsync();
//         Assert.Contains(expected, actual);
//     }
//
//     [Fact]
//     public async Task GIVEN_Zero_Length_THEN_Get_Fails()
//     {
//         PageQuery pageQuery = new(0, 0);
//         var sut = Services.GetRequiredService<IRepository<Tenant>>();
//         await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
//             await sut
//             .Get(pageQuery, CancellationToken.None)
//             .ToListAsync()
//         );
//     }
// }



