using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Threads.Infrastracture.Services;

public static class UploadConfigurator
{
    private const long MaxUploadSizeInBytes = 104_857_600;

    public static void Configure(KestrelServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Limits.MaxRequestBodySize = MaxUploadSizeInBytes;
    }

    public static void Configure(FormOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.MultipartBodyLengthLimit = MaxUploadSizeInBytes;
    }
}
