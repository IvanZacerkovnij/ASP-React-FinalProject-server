using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Search;

namespace Threads.Application.DTOs.Validation;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ValidSearchDateRangeAttribute : ValidationAttribute
{
    public ValidSearchDateRangeAttribute()
        : base("FromDate must be earlier than or equal to ToDate.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is not SearchPostsRequest request ||
               !request.FromDate.HasValue ||
               !request.ToDate.HasValue ||
               request.FromDate <= request.ToDate;
    }
}
