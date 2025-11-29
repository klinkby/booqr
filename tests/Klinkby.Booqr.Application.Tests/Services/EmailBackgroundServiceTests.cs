using System.Threading.Channels;
using Klinkby.Booqr.Application.Services;
using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Application.Tests.Services;

public class EmailBackgroundServiceTests
{
    [Theory]
    [AutoData]
    public async Task GIVEN_ChannelWithMessage_WHEN_Started_THEN_CallsMailClientSend(Message message)
    {
        // Arrange
        var channel = Channel.CreateUnbounded<Message>();
        var mailClientMock = new Mock<IMailClient>(MockBehavior.Strict);
        mailClientMock
            .Setup(m => m.Send(message, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var sut = new EmailBackgroundService(
            channel.Reader,
            mailClientMock.Object,
            NullLogger<EmailBackgroundService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await sut.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(message, cts.Token);
        channel.Writer.Complete();

        // Assert
        await Task.Delay(100, cts.Token); // Allow background processing
        mailClientMock.Verify(m => m.Send(message, It.IsAny<CancellationToken>()), Times.Once);
        await sut.StopAsync(CancellationToken.None);
    }
}
