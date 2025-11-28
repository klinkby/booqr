using System.Net;
using AutoFixture.Xunit3;
using Klinkby.Booqr.Core.Exceptions;
using Klinkby.Booqr.Infrastructure.Models;
using Klinkby.Booqr.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Klinkby.Booqr.Infrastructure.Tests;

public class EmailLabsMailClientTests
{
    private const string SmtpAccount = "test-account";
    private const string FromAddress = "noreply@test.com";
    private const string BaseAddress = "https://127.0.0.1";

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Used outside of factory")]
    private static EmailLabsMailClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseAddress)
        };

        var options = Options.Create(new InfrastructureSettings
        {
            ConnectionString = "foo",
            MailClientApiKey = "bar:baz",
            MailClientAccount = SmtpAccount,
            MailClientBaseAddress = new Uri(BaseAddress),
            MailClientFromAddress = FromAddress
        });

        return new EmailLabsMailClient(httpClient, options, NullLogger<EmailLabsMailClient>.Instance);
    }

    [Theory, AutoData]
    public async Task GIVEN_ValidMessage_WHEN_Send_THEN_SuccessfullySendsEmail(Message message)
    {
        // Arrange
        using var okMessage = new HttpResponseMessage(HttpStatusCode.OK);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(okMessage);

        var client = CreateClient(handlerMock.Object);

        // Act
        await client.Send(message);

        // Assert
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory, AutoData]
    public async Task GIVEN_FailureResponse_WHEN_Send_THEN_ThrowsMailClientException(Message message)
    {
        // Arrange
        var responseContent = new StringContent(
            """{"code": 1001, "status": "error", "message": "Invalid recipient"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        using var badResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        badResponse.Content = responseContent;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(badResponse);

        var client = CreateClient(handlerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<MailClientException>(() => client.Send(message));
        Assert.Equal(1001, exception.Code);
        Assert.Equal("error", exception.Status);
    }

    [Theory, AutoData]
    public async Task GIVEN_NetworkError_WHEN_Send_THEN_ThrowsMailClientException(Message message)
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No connection"));

        var client = CreateClient(handlerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<MailClientException>(() => client.Send(message));
    }
}
