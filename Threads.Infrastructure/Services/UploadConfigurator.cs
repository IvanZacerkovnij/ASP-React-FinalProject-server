using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Threads.Infrastructure.Services;

public static class UploadConfigurator
{
    private const long MaxUploadSizeInBytes = 257 * 1024 * 1024;

    public static void Configure(KestrelServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Limits.MaxRequestBodySize = MaxUploadSizeInBytes;
    }

    public static void Configure(FormOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.MultipartBodyLengthLimit = 256 * 1024 * 1024;
    }
}
