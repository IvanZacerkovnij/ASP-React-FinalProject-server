using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotInFutureDateAttribute : ValidationAttribute
{
    public NotInFutureDateAttribute()
        : base("The {0} field must not be in the future.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is null ||
               value is DateOnly date && date <= DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
