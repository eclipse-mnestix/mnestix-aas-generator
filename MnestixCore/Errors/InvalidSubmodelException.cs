namespace MnestixCore.Errors;

public class InvalidSubmodelException : Exception
{
    public ErrorCodes ErrorCode { get; }

    public InvalidSubmodelException(ErrorCodes errorCode)
    {
        ErrorCode = errorCode;
    }

    public InvalidSubmodelException(ErrorCodes errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public InvalidSubmodelException(ErrorCodes errorCode, string message, Exception inner)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}