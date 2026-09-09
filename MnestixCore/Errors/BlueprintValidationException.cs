using MnestixCore.TemplateBuilder;

namespace MnestixCore.Errors;

public class BlueprintValidationException : AasGeneratorException
{
    public override AasGeneratorErrorCode Code => AasGeneratorErrorCode.BlueprintValidationFailed;

    public IReadOnlyList<BlueprintValidationError> Errors { get; }

    public BlueprintValidationException(IReadOnlyList<BlueprintValidationError> errors)
        : base($"Blueprint validation failed with {errors.Count} error(s).")
    {
        Errors = errors;
    }

    public override AasGeneratorErrorDto ToErrorDto() =>
        new(Code, Message, new ValidationErrorContext(Errors));
}
