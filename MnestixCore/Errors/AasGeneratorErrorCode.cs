using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MnestixCore.Errors;

[JsonConverter(typeof(StringEnumConverter))]
public enum AasGeneratorErrorCode
{
    MappingFailed,
    BlueprintValidationFailed,
    RepositoryOperationFailed,
    InvalidBlueprint,
    InvalidInput,
    UnknownError
}
