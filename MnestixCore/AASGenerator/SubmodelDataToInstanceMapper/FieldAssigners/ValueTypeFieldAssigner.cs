using MnestixCore.Errors;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the "valueType" field. The resolved value must be a recognized AAS DataTypeDefXsd value;
/// otherwise generation fails. Assigned before "value" so that value content can be validated against it.
/// </summary>
public sealed class ValueTypeFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "valueType";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        var valueType = resolvedValue.ToString();
        if (!DataTypeDefXsd.IsValid(valueType))
        {
            throw new SubmodelDataToInstanceMapperException(
                $"valueType '{valueType}' is not a valid AAS DataTypeDefXsd. Allowed: {string.Join(", ", DataTypeDefXsd.All)}", ctx);
        }

        WarnIfOverridingDefault(element, this.FieldName, ctx);
        element[this.FieldName] = valueType;
    }
}
