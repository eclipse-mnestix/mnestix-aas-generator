using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the "semanticId" field. A resolved string is wrapped into an ExternalReference with a
/// single GlobalReference key; a resolved JSON object is treated as an existing AAS Reference and used as-is.
/// </summary>
public sealed class SemanticIdFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "semanticId";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        WarnIfOverridingDefault(element, this.FieldName, ctx);

        if (resolvedValue is JObject reference)
        {
            element[this.FieldName] = reference;
            return;
        }

        var value = resolvedValue.ToString();
        element[this.FieldName] = new JObject
        {
            ["type"] = "ExternalReference",
            ["keys"] = new JArray
            {
                new JObject
                {
                    ["type"] = "GlobalReference",
                    ["value"] = value
                }
            }
        };
    }
}
