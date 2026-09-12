namespace Threads.Application.Exceptions;

public sealed class ExternalServiceException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
