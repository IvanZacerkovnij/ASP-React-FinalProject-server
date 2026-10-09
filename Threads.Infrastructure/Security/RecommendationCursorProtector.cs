using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Recommendations;

namespace Threads.Infrastructure.Security;

public sealed class RecommendationCursorProtector(IDataProtectionProvider provider) : IRecommendationCursorProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Threads.Recommendations.Cursor.v1");

    public string Protect(string value)
    {
        using var buffer = new MemoryStream();
        using (var compressor = new BrotliStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(Encoding.UTF8.GetBytes(value));
        }

        return WebEncoders.Base64UrlEncode(_protector.Protect(buffer.ToArray()));
    }

    public string Unprotect(string value)
    {
        try
        {
            var compressed = _protector.Unprotect(WebEncoders.Base64UrlDecode(value));
            using var buffer = new MemoryStream(compressed);
            using var decompressor = new BrotliStream(buffer, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressor, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or InvalidDataException)
        {
            throw new RequestValidationException("Recommendation cursor is invalid.");
        }
    }
}
