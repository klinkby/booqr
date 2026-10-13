using System.ComponentModel.DataAnnotations;
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
            TestHelpers.CreateTenantContext(TenantId),
            NullLogger<SignUpCommand>.Instance);

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
        var expectedEmail = email;
        // ActivityRecorder is a static mock shared across this theory's cases; reset so the
        // Times.Once verification below only reflects this invocation.
        ActivityRecorder.Invocations.Clear();

        User? capturedUser = null;
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync(newUserId);
        _users.Setup(x => x.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user with { Id = newUserId, Email = expectedEmail });

        var request = new SignUpRequest(email, "  Jane Doe ", 12345678, "https://localhost");

        SignUpCommand sut = CreateSut();

        // Act
        var result = await sut.Execute(request);

        // Assert
        Assert.Equal(newUserId, result);
        Assert.NotNull(capturedUser);
        Assert.Equal(expectedEmail, capturedUser!.Email);
        Assert.Equal(UserRole.Customer, capturedUser.Role);
        Assert.Equal("Jane Doe", capturedUser.Name);
        Assert.Equal(12345678, capturedUser.Phone);

        bool hasMessage = _channel.Reader.TryRead(out Message? message);
        Assert.True(hasMessage && message is not null);

        Assert.Equal(expectedEmail, message.To);
        Assert.Contains(user.Name!, message.Body, StringComparison.InvariantCulture);
        Assert.Contains(TestHelpers.TenantName, message.Body, StringComparison.InvariantCulture);

        // The Phase 1 placeholder (tenant_id=0) must now carry the host-resolved tenant from
        // ITenantContext.
        ActivityRecorder.Verify(
            x => x.Add(It.Is<ActivityQuery<User>>(q => q.TenantId == TenantId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_DuplicateEmail_WHEN_Execute_THEN_NoSignUpMessageIsSent()
    {
        // Arrange: the unique (tenant_id, email) index makes the repository fail the second insert
        _users.Setup(x => x.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("duplicate email"));
        SignUpCommand sut = CreateSut();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Execute(new SignUpRequest("user@example.com", "Jane Doe", 12345678, "https://localhost")));

        // Assert
        Assert.False(_channel.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("Jane Doe", 12345678L, true)]
    [InlineData("", 12345678L, false)]
    [InlineData("  ", 12345678L, false)]
    [InlineData(null, 12345678L, false)]
    [InlineData("Jane Doe", null, false)]
    [InlineData("Jane Doe", 3_520_999L, false)]
    public void GIVEN_NameAndPhone_WHEN_Validate_THEN_BothRequired(string? name, long? phone, bool expected)
    {
        SignUpRequest request = new("user@example.com", name!, phone, "https://localhost");

        Assert.Equal(expected,
            Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"  \"")]
    public void GIVEN_NullOrBlankPhoneJson_WHEN_DeserializeAndValidate_THEN_Invalid(string phoneJson)
    {
        SignUpRequest? request = System.Text.Json.JsonSerializer.Deserialize<SignUpRequest>(
            $$"""{"email":"user@example.com","name":"n","phone":{{phoneJson}}}""", System.Text.Json.JsonSerializerOptions.Web);

        Assert.NotNull(request);
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }
}
