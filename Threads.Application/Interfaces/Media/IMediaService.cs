using Threads.Application.DTOs.Media;

namespace Threads.Application.Interfaces.Media;

public interface IMediaService
{
    Task<MediaAttachmentResponse?> GetByIdAsync(
        Guid mediaId,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);

    Task<MediaAttachmentResponse> UploadAsync(
        Guid uploadedByUserId,
        Stream content,
        string fileName,
        string contentType,
        long sizeInBytes,
        CancellationToken cancellationToken = default);
}
