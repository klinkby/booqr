using System.ComponentModel.DataAnnotations;
using Klinkby.Booqr.Application.Util;

namespace Klinkby.Booqr.Application.Tests;

public class Base64HmacKeyAttributeTests
{
    [Fact]
    public void IsValid_WithValidBase64Key_ReturnsSuccess()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var validKey = Convert.ToBase64String(new byte[32]); // 32 bytes minimum
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(validKey, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithKeyLongerThan32Bytes_ReturnsSuccess()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var validKey = Convert.ToBase64String(new byte[48]); // 48 bytes (HMACSHA3_384)
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(validKey, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithKeyLessThan32Bytes_ReturnsValidationError()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var shortKey = Convert.ToBase64String(new byte[16]); // Only 16 bytes
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(shortKey, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("at least 32 bytes", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("16 bytes", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void IsValid_WithInvalidBase64String_ReturnsValidationError()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var invalidKey = "not-valid-base64!!!";
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(invalidKey, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("valid Base64 string", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void IsValid_WithNull_ReturnsSuccess()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(null, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithEmptyString_ReturnsSuccess()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(string.Empty, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithWhitespace_ReturnsSuccess()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute();
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult("   ", context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithCustomMinimumKeySize_EnforcesCustomSize()
    {
        // Arrange
        var attribute = new Base64HmacKeyAttribute { MinimumKeySize = 64 };
        var key = Convert.ToBase64String(new byte[48]); // 48 bytes, less than 64
        var context = new ValidationContext(new object()) { DisplayName = "TestKey" };

        // Act
        var result = attribute.GetValidationResult(key, context);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("at least 64 bytes", result.ErrorMessage, StringComparison.Ordinal);
    }
}
