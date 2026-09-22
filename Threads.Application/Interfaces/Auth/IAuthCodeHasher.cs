namespace Threads.Application.Interfaces.Auth;

public interface IAuthCodeHasher
{
    string Hash(string code, string context);
    bool Verify(string code, string context, string expectedHash);
}