using System.ComponentModel.DataAnnotations;
using Threads.Api.Requests.Validation;

namespace Threads.Api.Requests.Media;

public class UploadMediaRequest
{
    [Required, MediaUploadFile]
    public IFormFile File { get; set; } = null!;
}
