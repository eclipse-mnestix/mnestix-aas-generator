namespace MnestixCore.TemplateBuilder;

public sealed record BlueprintValidationError(
    BlueprintValidationRule Rule,
    string Path,
    string Message);
