using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns idShort with sanitization to conform to AAS Metamodel v3: [a-zA-Z][a-zA-Z0-9_]*
/// </summary>
public sealed class IdShortFieldAssigner : FieldAssignerBase
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

        if (sanitized != value)
        {
            ctx.LogWarning($"idShort value '{value}' was sanitized to '{sanitized}'");
        }

        element["idShort"] = sanitized;
    }
}
