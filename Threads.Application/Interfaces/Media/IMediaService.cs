using Threads.Application.DTOs.Media;

namespace Threads.Application.Interfaces.Media;

public interface IMediaService
{
    Task<UploadMediaResponse> UploadAsync(
        Guid uploadedByUserId,
        Stream content,
        string fileName,
        string contentType,
        long sizeInBytes,
        CancellationToken cancellationToken = default);
}
