using System.ComponentModel.DataAnnotations;
using Threads.Application.Exceptions;

namespace Threads.Application.Services.Common;

internal static class RequestValidator
{
    public static void Validate<TRequest>(TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true))
        {
            return;
        }

        throw new RequestValidationException(
            validationResults[0].ErrorMessage ?? "The request is invalid.");
    }
}
