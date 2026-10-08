namespace Threads.Api.Exceptions;

public sealed class InvalidUserClaimsException : Exception
{
    public InvalidUserClaimsException()
        : base("Invalid token claims.")
    {
    }
}
