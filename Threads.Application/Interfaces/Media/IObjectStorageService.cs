namespace Threads.Application.Interfaces.Media;

public interface IObjectStorageService
{
    Task UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    string GetReadUrl(string objectKey);
}
