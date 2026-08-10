using Threads.Application.DTOs.Media;

namespace Threads.Application.Interfaces.Media;

public interface IMediaService
{
    Task<MediaUrlResponse?> GetUrlAsync(
        Guid mediaId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<UploadMediaResponse> UploadAsync(
        Guid uploadedByUserId,
        Stream content,
        string fileName,
        string contentType,
        long sizeInBytes,
        CancellationToken cancellationToken = default);
}
