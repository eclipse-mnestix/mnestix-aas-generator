using Newtonsoft.Json.Linq;

namespace MnestixCore.TemplateBuilder;

public sealed class BlueprintValidator : IBlueprintValidator
{
    public IReadOnlyList<BlueprintValidationError> Validate(JObject blueprint)
    {
        throw new NotImplementedException();
    }
}
