using System.Text.RegularExpressions;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns idShort with sanitization to conform to AAS Metamodel v3: [a-zA-Z][a-zA-Z0-9_]*
/// </summary>
internal sealed class IdShortFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "idShort";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        var value = resolvedValue.ToString();
        var sanitized = Regex.Replace(value, @"[^a-zA-Z0-9_]", "_");

        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]))
        {
            sanitized = "i" + sanitized;
        }

        if (string.IsNullOrEmpty(sanitized))
        {
            throw new SubmodelDataToInstanceMapperException(
                $"'{this.FieldName}' value '{value}' cannot be sanitized to a valid idShort (must contain at least one letter or digit)", ctx);
        }
        else if (sanitized != value)
        {
            ctx.LogWarning($"{this.FieldName} value '{value}' was sanitized to '{sanitized}'");
        }

        WarnIfOverridingDefault(element, this.FieldName, ctx);
        element[this.FieldName] = sanitized;
    }
}
