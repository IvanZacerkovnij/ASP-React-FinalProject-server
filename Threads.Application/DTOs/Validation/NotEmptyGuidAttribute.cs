using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute()
        : base("The {0} field must not be an empty GUID.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is null || value is Guid guid && guid != Guid.Empty;
    }
}
