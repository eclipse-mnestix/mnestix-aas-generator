using MnestixCore.TemplateBuilder;

namespace MnestixCore.Errors;

public class BlueprintValidationException : Exception
{
    public IReadOnlyList<BlueprintValidationError> Errors { get; }

    public BlueprintValidationException(IReadOnlyList<BlueprintValidationError> errors)
        : base($"Blueprint validation failed with {errors.Count} error(s).")
    {
        Errors = errors;
    }
}
