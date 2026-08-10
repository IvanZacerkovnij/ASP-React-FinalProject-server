namespace Threads.Application.Exceptions;

public sealed class MediaProcessingException : Exception
{
    public MediaProcessingException(string message)
        : base(message)
    {
    }

    public MediaProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
