namespace MnestixCore.Errors;

public abstract class AasGeneratorException : Exception
{
    public abstract AasGeneratorErrorCode Code { get; }
    public abstract AasGeneratorErrorDto ToErrorDto();

    protected AasGeneratorException(string message) : base(message) { }

    protected AasGeneratorException(string message, Exception innerException) : base(message, innerException) { }
}
