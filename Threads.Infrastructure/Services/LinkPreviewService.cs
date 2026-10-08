using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.LinkPreviews;
using Threads.Application.Services.LinkPreviews;

namespace Threads.Infrastructure.Services;

public sealed partial class LinkPreviewService : ILinkPreviewService
{
    private const int MaxRedirects = 5;
    private const int MaxHtmlCharacters = 1_000_000;
    private const int MaxTitleLength = 255;

    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkPreviewService> _logger;

    public LinkPreviewService(
        HttpClient httpClient,
        ILogger<LinkPreviewService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LinkPreviewResponse> ResolveAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var currentUri = ParseUri(url);

        try
        {
            for (var redirectCount = 0; redirectCount <= MaxRedirects; redirectCount++)
            {
                await EnsurePublicDestinationAsync(currentUri, cancellationToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (IsRedirect(response.StatusCode))
                {
                    if (redirectCount == MaxRedirects || response.Headers.Location is null)
                    {
                        throw new ExternalServiceException("Link preview redirect limit was exceeded.");
                    }

                    currentUri = ResolveRedirectUri(currentUri, response.Headers.Location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ExternalServiceException(
                        $"Link preview source returned status code {(int)response.StatusCode}.");
                }

                EnsureHtmlContent(response.Content.Headers.ContentType?.MediaType);
                var html = await ReadHtmlAsync(response.Content, cancellationToken);
                var metadata = ParseMetadata(html);

                return LinkPreviewResponseFactory.Create(
                    currentUri.AbsoluteUri,
                    metadata.Title,
                    metadata.ImageUrl);
            }
        }
        catch (RequestValidationException)
        {
            throw;
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or SocketException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Unable to resolve link preview for {Url}", currentUri);
            throw new ExternalServiceException("Unable to resolve the link preview.", exception);
        }

        throw new ExternalServiceException("Unable to resolve the link preview.");
    }

    private static Uri ParseUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            url.Length > 2048 ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            !IsHttpUri(uri) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new RequestValidationException("A valid public HTTP or HTTPS URL is required.");
        }

        return uri;
    }

    private static async Task EnsurePublicDestinationAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (uri.IsLoopback ||
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Local link preview URLs are not allowed.");
        }

        IPAddress[] addresses;

        try
        {
            addresses = IPAddress.TryParse(uri.DnsSafeHost, out var address)
                ? [address]
                : await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new ExternalServiceException("Unable to resolve the link preview host.", exception);
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new RequestValidationException("Private link preview URLs are not allowed.");
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return !IPAddress.IsLoopback(address) &&
                   !address.IsIPv6LinkLocal &&
                   !address.IsIPv6Multicast &&
                   !address.Equals(IPAddress.IPv6Any) &&
                   (bytes[0] & 0xFE) != 0xFC;
        }

        var octets = address.GetAddressBytes();
        return octets[0] switch
        {
            0 or 10 or 127 => false,
            100 when octets[1] is >= 64 and <= 127 => false,
            169 when octets[1] == 254 => false,
            172 when octets[1] is >= 16 and <= 31 => false,
            192 when octets[1] == 168 => false,
            198 when octets[1] is 18 or 19 => false,
            >= 224 => false,
            _ => true
        };
    }

    private static Uri ResolveRedirectUri(Uri currentUri, Uri location)
    {
        var redirectUri = location.IsAbsoluteUri
            ? location
            : new Uri(currentUri, location);

        return ParseUri(redirectUri.AbsoluteUri);
    }

    private static void EnsureHtmlContent(string? mediaType)
    {
        if (mediaType is null ||
            mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new RequestValidationException("The URL does not point to an HTML document.");
    }

    private static async Task<string> ReadHtmlAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        var remaining = MaxHtmlCharacters;
        var html = new System.Text.StringBuilder(Math.Min(remaining, 32_768));

        while (remaining > 0)
        {
            var read = await reader.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            html.Append(buffer, 0, read);
            remaining -= read;
        }

        return html.ToString();
    }

    private static LinkPreviewMetadata ParseMetadata(string html)
    {
        string? title = null;
        string? imageUrl = null;

        foreach (Match tagMatch in MetaTagRegex().Matches(html))
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match attributeMatch in AttributeRegex().Matches(tagMatch.Value))
            {
                attributes[attributeMatch.Groups["name"].Value] =
                    WebUtility.HtmlDecode(GetAttributeValue(attributeMatch));
            }

            if (!attributes.TryGetValue("content", out var content) ||
                string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            attributes.TryGetValue("property", out var property);
            attributes.TryGetValue("name", out var name);
            var key = property ?? name;

            if (title is null && IsMetadataKey(key, "og:title", "twitter:title"))
            {
                title = content;
            }
            else if (imageUrl is null && IsMetadataKey(key, "og:image", "twitter:image"))
            {
                imageUrl = content;
            }
        }

        if (title is null)
        {
            var titleMatch = TitleRegex().Match(html);
            title = titleMatch.Success
                ? WebUtility.HtmlDecode(titleMatch.Groups["value"].Value)
                : null;
        }

        title = NormalizeTitle(title);
        return new LinkPreviewMetadata(title, imageUrl);
    }

    private static string GetAttributeValue(Match match)
    {
        return match.Groups["doubleQuoted"].Success
            ? match.Groups["doubleQuoted"].Value
            : match.Groups["singleQuoted"].Success
                ? match.Groups["singleQuoted"].Value
                : match.Groups["unquoted"].Value;
    }

    private static bool IsMetadataKey(string? value, string first, string second)
    {
        return value?.Equals(first, StringComparison.OrdinalIgnoreCase) == true ||
               value?.Equals(second, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var normalizedTitle = WhitespaceRegex().Replace(title, " ").Trim();
        return normalizedTitle.Length <= MaxTitleLength
            ? normalizedTitle
            : normalizedTitle[..MaxTitleLength];
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently or
            HttpStatusCode.Found or
            HttpStatusCode.SeeOther or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static bool IsHttpUri(Uri uri)
    {
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<meta\\s+[^>]*>", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(
        "(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)\\s*=\\s*(?:\"(?<doubleQuoted>[^\"]*)\"|'(?<singleQuoted>[^']*)'|(?<unquoted>[^\\s\"'=<>`]+))",
        RegexOptions.NonBacktracking)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(
        "<title[^>]*>(?<value>.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("\\s+", RegexOptions.NonBacktracking)]
    private static partial Regex WhitespaceRegex();

    private sealed record LinkPreviewMetadata(string? Title, string? ImageUrl);
}
