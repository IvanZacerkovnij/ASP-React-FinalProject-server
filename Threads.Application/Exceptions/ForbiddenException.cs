namespace Threads.Application.Exceptions;

public sealed class ForbiddenException(string message) : Exception(message);