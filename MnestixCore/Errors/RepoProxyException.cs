namespace MnestixCore.Errors;

public class RepoProxyException : Exception
{
    public ErrorCodes ErrorCode { get; }

    public RepoProxyException(ErrorCodes errorCode)
    {
        ErrorCode = errorCode;
    }

    public RepoProxyException(ErrorCodes errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public RepoProxyException(ErrorCodes errorCode, string? message, Exception? inner)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}