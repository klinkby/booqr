namespace Klinkby.Booqr.Application.Util;

/// <summary>
/// Validates that a string is a valid Base64-encoded value with sufficient length for HMAC key security.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class Base64HmacKeyAttribute : ValidationAttribute
{
    private const int MinimumKeySizeInBytes = 32;

    /// <summary>
    /// Gets or sets the minimum required key size in bytes. Defaults to 32 bytes.
    /// </summary>
    public int MinimumKeySize { get; init; } = MinimumKeySizeInBytes;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult($"The {validationContext.DisplayName} field must be a string.");
        }

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return ValidationResult.Success;
        }

        // Validate Base64 format
        try
        {
            var keyBytes = Convert.FromBase64String(stringValue);
            
            // Validate minimum key length
            if (keyBytes.Length < MinimumKeySize)
            {
                return new ValidationResult(
                    $"The {validationContext.DisplayName} field must be at least {MinimumKeySize} bytes when decoded. Current length: {keyBytes.Length} bytes.");
            }

            return ValidationResult.Success;
        }
        catch (FormatException)
        {
            return new ValidationResult($"The {validationContext.DisplayName} field must be a valid Base64 string.");
        }
    }
}
