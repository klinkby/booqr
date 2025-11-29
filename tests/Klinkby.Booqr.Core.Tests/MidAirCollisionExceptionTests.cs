using Klinkby.Booqr.Core.Exceptions;
using AutoFixture.Xunit3;

namespace Klinkby.Booqr.Tests;

public class MidAirCollisionExceptionTests
{
    [Fact]
    public void DefaultCtor_ShouldCreateException()
    {
        // act
        var ex = new MidAirCollisionException();

        // assert
        Assert.IsType<MidAirCollisionException>(ex);
        Assert.Null(ex.InnerException);
        Assert.False(string.IsNullOrEmpty(ex.Message));
    }

    [Theory]
    [AutoData]
    public void MessageCtor_ShouldSetMessage(string message)
    {
        // act
        var ex = new MidAirCollisionException(message);

        // assert
        Assert.Equal(message, ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Theory]
    [AutoData]
    public void MessageAndInnerCtor_ShouldSetMessageAndInner(string message, InvalidOperationException inner)
    {
        // act
        var ex = new MidAirCollisionException(message, inner);

        // assert
        Assert.Equal(message, ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
