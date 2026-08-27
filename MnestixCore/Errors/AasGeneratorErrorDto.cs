namespace MnestixCore.Errors;

public record AasGeneratorErrorDto(
    AasGeneratorErrorCode Code,
    string Message,
    AasGeneratorErrorContext? Context
);
