using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the "second" field on RelationshipElements. Requires the resolved value to be a JObject (AAS Reference).
/// </summary>
public sealed class SecondFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "second";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        if (resolvedValue is not JObject refObj)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Field 'second' requires a JSON object (AAS Reference), but got {resolvedValue.Type}", ctx);
        }

        element["second"] = refObj;
    }
}
