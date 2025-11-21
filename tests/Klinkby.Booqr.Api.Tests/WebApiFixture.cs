using System.Text;
using System.Text.Unicode;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Klinkby.Booqr.Api.Tests;

internal sealed class WebApiFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string jsonConfig = """
        {
          "Application": {
            "Jwt": {
              "Key": "fa15a2b38ad08...fbbdba",
              "Issuer": "booqr",
              "Audience": "https://www.booqr.dk"
            },
            "Password": {
              "HmacKey": "WzE4MiwxOTksOTQsNzcsMjU0LDY2LDQ3LDIzMyw5MywxMjcsMjUsMTIyLDU0LDE0OCwyNCwzMywxODgsNjMsMjI1LDE0Nyw5NiwyMzksMTc3LDEyMywyMjQsMTI2LDE4NywyMTUsMTY1LDEyNCwyMjQsMjM2XQ==",
              "ResetPath": "/change-password",
              "ResetTimeoutHours": 2,
              "SignUpTimeoutHours": 24
            }
          },
          "Infrastructure": {
            "ConnectionString": "Host=postgres:5432;Database=postgres;Username=postgres;Password=...",
            "MailClientApiKey": "...:...",
            "MailClientAccount": "1.....smtp",
            "MailClientFromAddress": "no-reply@booqr.dk"
          }
        }
        """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(jsonConfig));
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
        builder.UseConfiguration(configuration);
        builder.ConfigureAppConfiguration(_ => { });
        builder.ConfigureTestServices(_ => { });
    }
}
