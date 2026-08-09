using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Threads.Application.Interfaces.Media;

namespace Threads.Infrastracture.Services;

public class S3ObjectStorageService : IObjectStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly int _readUrlExpirationMinutes;

    public S3ObjectStorageService(IConfiguration configuration)
    {
        var regionName = configuration["AWS:S3:Region"];
        _bucketName = configuration["AWS:S3:BucketName"]!;
        _readUrlExpirationMinutes = int.TryParse(
            configuration["AWS:S3:ReadUrlExpirationMinutes"],
            out var expirationMinutes)
            ? expirationMinutes
            : 60;

        var region = RegionEndpoint.GetBySystemName(regionName);
        _s3Client = new AmazonS3Client(region);
    }

    public async Task UploadAsync(
        Stream content,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);
    }

    public string GetReadUrl(string objectKey)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddMinutes(_readUrlExpirationMinutes)
        };

        return _s3Client.GetPreSignedURL(request);
    }
}
