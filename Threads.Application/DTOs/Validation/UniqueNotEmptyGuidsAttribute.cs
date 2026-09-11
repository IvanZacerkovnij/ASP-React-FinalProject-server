using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class UniqueNotEmptyGuidsAttribute : ValidationAttribute
{
    public UniqueNotEmptyGuidsAttribute()
        : base("The {0} field must contain unique, non-empty GUIDs.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not IEnumerable values)
        {
            return false;
        }

        var uniqueValues = new HashSet<Guid>();

        foreach (var item in values)
        {
            if (item is not Guid guid || guid == Guid.Empty || !uniqueValues.Add(guid))
            {
                return false;
            }
        }

        return true;
    }
}
