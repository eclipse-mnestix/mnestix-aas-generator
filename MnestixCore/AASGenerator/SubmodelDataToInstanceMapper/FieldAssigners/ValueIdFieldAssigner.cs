using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>Assigns the optional reference identifying a coded Property or MultiLanguageProperty value.</summary>
public sealed class ValueIdFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "valueId";

    public override bool IsResolvedValueMissing(JToken resolvedValue) =>
        resolvedValue.Type == JTokenType.Null
        || (resolvedValue.Type == JTokenType.String && string.IsNullOrEmpty(resolvedValue.Value<string>()));

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx) =>
        AssignExternalReference(element, resolvedValue, ctx);
}
