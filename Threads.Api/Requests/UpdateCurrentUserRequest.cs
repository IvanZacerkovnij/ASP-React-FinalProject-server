using Microsoft.AspNetCore.Http;
using Threads.Application.DTOs.Locations;

namespace Threads.Api.Requests;

public class UpdateCurrentUserRequest
{
    public string? DisplayName { get; set; }

    public string? Bio { get; set; }

    public bool RemoveLocation { get; set; }

    public LocationRequest? Location { get; set; }

    public bool RemoveAvatar { get; set; }

    public bool RemoveBanner { get; set; }

    public IFormFile? Avatar { get; set; }

    public IFormFile? Banner { get; set; }
}
