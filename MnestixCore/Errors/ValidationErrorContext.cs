using MnestixCore.TemplateBuilder;

namespace MnestixCore.Errors;

public record ValidationErrorContext(IReadOnlyList<BlueprintValidationError> Errors) : AasGeneratorErrorContext;
