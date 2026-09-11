using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class FutureDateTimeAttribute : ValidationAttribute
{
    public FutureDateTimeAttribute()
        : base("The {0} field must be in the future.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is null || value is DateTime dateTime && dateTime > DateTime.UtcNow;
    }
}
