using Newtonsoft.Json.Linq;

namespace MnestixCore.TemplateBuilder;

public interface IBlueprintValidator
{
    IReadOnlyList<BlueprintValidationError> Validate(JObject blueprint);
}
