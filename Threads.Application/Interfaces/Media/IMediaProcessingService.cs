using Threads.Application.DTOs.Media;

namespace Threads.Application.Interfaces.Media;

public interface IMediaProcessingService
{
    Task<MediaProcessingResult> ProcessAsync(
        string sourceFilePath,
        string contentType,
        CancellationToken cancellationToken = default);
}
