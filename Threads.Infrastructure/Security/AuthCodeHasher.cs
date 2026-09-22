using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Threads.Application.Interfaces.Auth;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Security;

public sealed class AuthCodeHasher : IAuthCodeHasher
{
    private const string ConfigurationKey = "AuthCodes:HashKey";
    private const int MinimumKeySizeInBytes = 32;

    private readonly byte[] _key;

    public AuthCodeHasher(IConfiguration configuration)
    {
        var encodedKey = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            throw new InfrastructureConfigurationException(ConfigurationKey);
        }

        try
        {
            _key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"Configuration key '{ConfigurationKey}' must contain a Base64-encoded value.",
                exception);
        }

        if (_key.Length < MinimumKeySizeInBytes)
        {
            throw new InvalidOperationException(
                $"Configuration key '{ConfigurationKey}' must contain at least {MinimumKeySizeInBytes} bytes.");
        }
    }

    public string Hash(string code, string context)
    {
        var hash = ComputeHash(code, context);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string code, string context, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        byte[] expectedHashBytes;

        try
        {
            expectedHashBytes = Convert.FromBase64String(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHashBytes = ComputeHash(code, context);
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }

    private byte[] ComputeHash(string code, string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var value = Encoding.UTF8.GetBytes($"{context}\0{code}");
        return HMACSHA256.HashData(_key, value);
    }
}
