using System.Globalization;
using System.Text;
using Threads.Application.DTOs.Pagination;
using Threads.Application.Exceptions;

namespace Threads.Application.Services;

internal static class CursorCodec
{
    private const char Separator = '|';

    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{createdAt:O}{Separator}{id:N}");

        return EncodeValue(value);
    }

    public static string EncodeText(string value, Guid id)
    {
        var encodedText = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return EncodeValue($"{encodedText}{Separator}{id:N}");
    }

    public static CursorPosition? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var value = DecodeValue(cursor);
            var parts = value.Split(Separator, StringSplitOptions.None);

            if (parts.Length != 2 ||
                !DateTimeOffset.TryParseExact(
                    parts[0],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var createdAt) ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException();
            }

            return new CursorPosition(createdAt, id);
        }
        catch (FormatException)
        {
            throw new RequestValidationException("Cursor is invalid.");
        }
    }

    public static TextCursorPosition? DecodeText(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var value = DecodeValue(cursor);
            var parts = value.Split(Separator, StringSplitOptions.None);

            if (parts.Length != 2 ||
                !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new FormatException();
            }

            var text = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException();
            }

            return new TextCursorPosition(text, id);
        }
        catch (FormatException)
        {
            throw new RequestValidationException("Cursor is invalid.");
        }
    }

    private static string EncodeValue(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string DecodeValue(string cursor)
    {
        var base64 = cursor.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
