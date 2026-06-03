using System.Net;

namespace MnestixCore.Errors;

public class RepoProxyException : Exception
{
    public ErrorCodes ErrorCode { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }

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

    public RepoProxyException(ErrorCodes errorCode, string? message, HttpStatusCode statusCode, string? responseBody)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}