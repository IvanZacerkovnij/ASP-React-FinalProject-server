using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TrimmedAllowedValueAttribute : ValidationAttribute
{
    private readonly string _allowedValue;

    public TrimmedAllowedValueAttribute(string allowedValue)
        : base("The {0} field has an unsupported value.")
    {
        _allowedValue = allowedValue;
    }

    public override bool IsValid(object? value)
    {
        return value is null ||
               value is string text &&
               (string.IsNullOrWhiteSpace(text) ||
                string.Equals(
                    text.Trim(),
                    _allowedValue,
                    StringComparison.OrdinalIgnoreCase));
    }
}
