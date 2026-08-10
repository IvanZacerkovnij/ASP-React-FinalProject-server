using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Threads.Application.DTOs.Media;
using Threads.Application.Interfaces.Media;

namespace Threads.Infrastracture.Services;

public class FfmpegMediaProcessingService : IMediaProcessingService
{
    private readonly string _ffprobePath;
    private readonly string _ffmpegPath;

    public FfmpegMediaProcessingService(IConfiguration configuration)
    {
        _ffprobePath = configuration["MediaProcessing:FfprobePath"] ?? "ffprobe";
        _ffmpegPath = configuration["MediaProcessing:FfmpegPath"] ?? "ffmpeg";
    }

    public async Task<MediaProcessingResult> ProcessAsync(
        string sourceFilePath,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var metadata = await ExtractMetadataAsync(sourceFilePath, cancellationToken);

        if (!contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return metadata;
        }

        var thumbnailFilePath = await TryGenerateThumbnailAsync(sourceFilePath, cancellationToken);

        return metadata with
        {
            ThumbnailFilePath = thumbnailFilePath
        };
    }

    private async Task<MediaProcessingResult> ExtractMetadataAsync(
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        var arguments = new[]
        {
            "-v",
            "error",
            "-select_streams",
            "v:0",
            "-show_entries",
            "stream=width,height:format=duration",
            "-of",
            "json",
            sourceFilePath
        };

        var output = await RunProcessAsync(_ffprobePath, arguments, cancellationToken);

        var probeResponse = JsonSerializer.Deserialize<ProbeResponse>(
            output,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        var stream = probeResponse?.Streams?.FirstOrDefault();

        if (stream is null)
        {
            throw new InvalidOperationException("Unable to extract media metadata.");
        }

        return new MediaProcessingResult
        {
            Width = stream.Width,
            Height = stream.Height,
            DurationSeconds = TryParseDuration(probeResponse?.Format?.Duration)
        };
    }

    private async Task<string?> TryGenerateThumbnailAsync(
        string sourceFilePath,
        CancellationToken cancellationToken)
    {
        var thumbnailFilePath = Path.Combine(
            Path.GetTempPath(),
            $"threads-thumbnail-{Guid.NewGuid():N}.jpg");

        var arguments = new[]
        {
            "-y",
            "-i",
            sourceFilePath,
            "-frames:v",
            "1",
            "-q:v",
            "2",
            thumbnailFilePath
        };

        var processStartInfo = BuildProcessStartInfo(_ffmpegPath, arguments);

        try
        {
            using var process = Process.Start(processStartInfo);

            if (process is null)
            {
                throw new InvalidOperationException("Unable to start ffmpeg.");
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || !File.Exists(thumbnailFilePath))
            {
                TryDeleteLocalFile(thumbnailFilePath);
                return null;
            }

            var thumbnailFile = new FileInfo(thumbnailFilePath);

            if (thumbnailFile.Length == 0)
            {
                TryDeleteLocalFile(thumbnailFilePath);
                return null;
            }

            return thumbnailFilePath;
        }
        catch
        {
            TryDeleteLocalFile(thumbnailFilePath);
            return null;
        }
    }

    private static double? TryParseDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        return double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDuration)
            ? parsedDuration
            : null;
    }

    private async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyCollection<string> arguments,
        CancellationToken cancellationToken)
    {
        var processStartInfo = BuildProcessStartInfo(fileName, arguments);

        using var process = Process.Start(processStartInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"Unable to start process '{fileName}'.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(standardError)
                ? "Unable to extract media metadata."
                : $"Unable to extract media metadata: {standardError.Trim()}";

            throw new InvalidOperationException(message);
        }

        return standardOutput;
    }

    private static ProcessStartInfo BuildProcessStartInfo(string fileName, IReadOnlyCollection<string> arguments)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        return processStartInfo;
    }

    private static void TryDeleteLocalFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            //
        }
    }

    private sealed class ProbeResponse
    {
        public List<ProbeStream>? Streams { get; init; }

        public ProbeFormat? Format { get; init; }
    }

    private sealed class ProbeStream
    {
        public int? Width { get; init; }

        public int? Height { get; init; }
    }

    private sealed class ProbeFormat
    {
        public string? Duration { get; init; }
    }
}
