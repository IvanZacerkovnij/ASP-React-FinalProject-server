using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidPollOptionsAttribute : ValidationAttribute
{
    private const int MaxOptionLength = 280;

    public ValidPollOptionsAttribute()
        : base("The {0} field must contain unique, non-empty options of at most 280 characters.")
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

        var uniqueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in values)
        {
            if (item is not string option)
            {
                return false;
            }

            var normalizedOption = option.Trim();

            if (normalizedOption.Length is 0 or > MaxOptionLength || !uniqueOptions.Add(normalizedOption))
            {
                return false;
            }
        }

        return true;
    }
}
