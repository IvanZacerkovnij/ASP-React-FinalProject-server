using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Users;

public class UpdateUserRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? DisplayName { get; init; }

    [StringLength(500, MinimumLength = 1)]
    public string? Bio { get; init; }

    [NotInFutureDate]
    public DateOnly? DateOfBirth { get; init; }

    [DefaultValue(false)]
    public bool RemoveDateOfBirth { get; init; }

    [DefaultValue(false)]
    public bool RemoveLocation { get; init; }

    public LocationRequest? Location { get; init; }

    [DefaultValue(false)]
    public bool RemoveAvatar { get; init; }

    [DefaultValue(false)]
    public bool RemoveBanner { get; init; }
}
