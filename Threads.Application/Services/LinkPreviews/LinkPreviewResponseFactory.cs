using System.Security.Cryptography;
using System.Text;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.Exceptions;

namespace Threads.Application.Services.LinkPreviews;

public static class LinkPreviewResponseFactory
{
    public static LinkPreviewResponse Create(
        string url,
        string? title,
        string? imageUrl)
    {
        var normalizedUrl = url.Trim();

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) ||
            !IsHttpUri(uri))
        {
            throw new RequestValidationException("A valid HTTP or HTTPS URL is required.");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri));

        return new LinkPreviewResponse
        {
            Id = $"preview-{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}",
            Url = uri.AbsoluteUri,
            Domain = uri.IdnHost.ToLowerInvariant(),
            Title = NormalizeOptionalValue(title),
            ImageUrl = NormalizeImageUrl(uri, imageUrl)
        };
    }

    private static string? NormalizeImageUrl(Uri pageUri, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !Uri.TryCreate(pageUri, imageUrl.Trim(), out var resolvedUri) ||
            !IsHttpUri(resolvedUri))
        {
            return null;
        }

        return resolvedUri.AbsoluteUri;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsHttpUri(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
