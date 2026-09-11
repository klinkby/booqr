using System.Threading.Channels;
using Klinkby.Booqr.Core;
using Microsoft.Extensions.Options;

namespace Klinkby.Booqr.Application.Tests.Commands;

public class SignUpCommandTests
{
    private readonly static TimeProvider TimeProvider = TestHelpers.TimeProvider;
    private readonly static Mock<IActivityRecorder> ActivityRecorder = new();
    private const int TenantId = 7;

    private readonly Mock<IUserRepository> _users = new();
    private readonly Channel<Message> _channel = Channel.CreateBounded<Message>(100);

    private SignUpCommand CreateSut() =>
        new(
            _users.Object,
            CreateExpiringQueryString(TimeProvider),
            _channel.Writer,
            ActivityRecorder.Object,
            Options.Create(new PasswordSettings { HmacKey = "" }),
            CreateTenantContext(),
            NullLogger<SignUpCommand>.Instance);

    private static ITenantContext CreateTenantContext()
    {
        var mock = new Mock<ITenantContext>();
        mock.SetupGet(x => x.HasTenant).Returns(true);
        mock.SetupGet(x => x.TenantId).Returns(TenantId);
        return mock.Object;
    }

    [Fact]
    public async Task GIVEN_NullRequest_WHEN_Execute_THEN_ThrowsArgumentNullException()
    {
        // Arrange
        SignUpCommand sut = CreateSut();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.Execute(null!));
    }

    [Theory]
    [InlineAutoData("  user@example.com  ")]
    [InlineAutoData("USER@EXAMPLE.COM")]
    public async Task GIVEN_ValidRequest_WHEN_Execute_THEN_MapsAndCallsRepository(string email, User user)
    {
        // Arrange
        const int newUserId = 987;
        var expectedEmail = email.Trim();
        // ActivityRecorder is a static mock shared across this theory's cases; reset so the
        // Times.Once verification below only reflects this invocation.
        ActivityRecorder.Invocations.Clear();

        User? capturedUser = null;
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync(newUserId);
        _users.Setup(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user with { Id = newUserId, Email = expectedEmail });

        var request = new SignUpRequest(email, "https://localhost");

        SignUpCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.Equal(newUserId, result);
        Assert.NotNull(capturedUser);
        Assert.Equal(expectedEmail, capturedUser!.Email);
        Assert.Equal(UserRole.Customer, capturedUser.Role);

        bool hasMessage = _channel.Reader.TryRead(out Message? message);
        Assert.True(hasMessage && message is not null);

        Assert.Equal(expectedEmail, message.To);
        Assert.Contains(expectedEmail,  message.Body, StringComparison.InvariantCulture);

        // The Phase 1 placeholder (tenant_id=0) must now carry the host-resolved tenant from
        // ITenantContext.
        ActivityRecorder.Verify(
            x => x.Add(It.Is<ActivityQuery<User>>(q => q.TenantId == TenantId)),
            Times.Once);
    }
}
