using SharedKernel;

namespace Application.Exceptions;

public sealed class NoteAppException(
    string requestName,
    Error? error = default,
    Exception? innerException = default)
    : Exception("Application exception", innerException)
{
    public string RequestName { get; } = requestName;
    public Error? Error { get; } = error;
}