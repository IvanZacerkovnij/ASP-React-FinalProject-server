using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Threads.Api.Requests.Validation;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Validation;

namespace Threads.Api.Requests.Users;

public class UpdateCurrentUserRequest
{
    [StringLength(100, MinimumLength = 1)]
    public string? DisplayName { get; set; }

    [StringLength(500, MinimumLength = 1)]
    public string? Bio { get; set; }

    [NotInFutureDate]
    public DateOnly? DateOfBirth { get; set; }

    [DefaultValue(false)]
    public bool RemoveDateOfBirth { get; set; }

    [DefaultValue(false)]
    public bool RemoveLocation { get; set; }

    public LocationRequest? Location { get; set; }

    [DefaultValue(false)]
    public bool RemoveAvatar { get; set; }

    [DefaultValue(false)]
    public bool RemoveBanner { get; set; }

    [ProfileImageFile]
    public IFormFile? Avatar { get; set; }

    [ProfileImageFile]
    public IFormFile? Banner { get; set; }
}
