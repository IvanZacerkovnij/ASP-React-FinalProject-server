using Microsoft.AspNetCore.Http;

namespace Threads.Api.Requests;

public class UploadMediaRequest
{
    public IFormFile File { get; set; } = null!;
}
