namespace MnestixCore.Errors;

public class InvalidBlueprintException : AasGeneratorException
{
    public override AasGeneratorErrorCode Code => AasGeneratorErrorCode.InvalidBlueprint;

    public InvalidBlueprintException(string message) : base(message) { }

    public InvalidBlueprintException(string message, Exception innerException) : base(message, innerException) { }

    public override AasGeneratorErrorDto ToErrorDto() => new(Code, Message, null);
}
