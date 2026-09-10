namespace MnestixCore.Errors;

public class RepositoryOperationFailedException : AasGeneratorException
{
    public override AasGeneratorErrorCode Code => AasGeneratorErrorCode.RepositoryOperationFailed;

    public RepositoryOperationFailedException(string message) : base(message) { }

    public RepositoryOperationFailedException(string message, Exception innerException) : base(message, innerException) { }

    public override AasGeneratorErrorDto ToErrorDto() => new(Code, Message, null);
}
