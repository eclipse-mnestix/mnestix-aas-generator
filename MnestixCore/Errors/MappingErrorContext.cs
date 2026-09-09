namespace MnestixCore.Errors;

public record MappingErrorContext(string? Qualifier, string? QualifierPath) : AasGeneratorErrorContext;
