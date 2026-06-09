using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the "first" field on RelationshipElements. Requires the resolved value to be a JObject (AAS Reference).
/// </summary>
internal sealed class FirstFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "first";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        if (resolvedValue is not JObject refObj)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Field '{this.FieldName}' requires a JSON object (AAS Reference), but got {resolvedValue.Type}", ctx);
        }

        WarnIfOverridingDefault(element, this.FieldName, ctx);
        element[this.FieldName] = refObj;
    }
}
