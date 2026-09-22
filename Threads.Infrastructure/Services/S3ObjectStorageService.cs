using System.Diagnostics;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Services;

public class S3ObjectStorageService : IObjectStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly int _readUrlExpirationMinutes;
    private readonly ILogger<S3ObjectStorageService> _logger;

    public S3ObjectStorageService(
        IConfiguration configuration,
        ILogger<S3ObjectStorageService> logger)
    {
        _logger = logger;
        var regionName = configuration["AWS:S3:Region"] ??
                         throw new InfrastructureConfigurationException("AWS:S3:Region");
        _bucketName = configuration["AWS:S3:BucketName"] ??
                      throw new InfrastructureConfigurationException("AWS:S3:BucketName");

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

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _s3Client.PutObjectAsync(request, cancellationToken);
            _logger.LogDebug(
                "S3 object {ObjectKey} uploaded in {ElapsedMilliseconds} ms",
                objectKey,
                stopwatch.ElapsedMilliseconds);
        }
        catch (AmazonClientException exception)
        {
            _logger.LogWarning(
                exception,
                "S3 object {ObjectKey} upload failed after {ElapsedMilliseconds} ms",
                objectKey,
                stopwatch.ElapsedMilliseconds);
            throw new ExternalServiceException("Unable to upload object to S3.", exception);
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _s3Client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken);
            _logger.LogDebug(
                "S3 object {ObjectKey} deleted in {ElapsedMilliseconds} ms",
                objectKey,
                stopwatch.ElapsedMilliseconds);
        }
        catch (AmazonClientException exception)
        {
            _logger.LogWarning(
                exception,
                "S3 object {ObjectKey} deletion failed after {ElapsedMilliseconds} ms",
                objectKey,
                stopwatch.ElapsedMilliseconds);
            throw new ExternalServiceException("Unable to delete object from S3.", exception);
        }
    }

    public string GetReadUrl(string objectKey)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.AddMinutes(_readUrlExpirationMinutes)
        };

        try
        {
            return _s3Client.GetPreSignedURL(request);
        }
        catch (AmazonClientException exception)
        {
            _logger.LogWarning(exception, "S3 read URL generation failed for object {ObjectKey}", objectKey);
            throw new ExternalServiceException("Unable to generate an S3 read URL.", exception);
        }
    }
}
