using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the "semanticId" field. A resolved scalar (string, number, boolean) is wrapped into an
/// ExternalReference with a single GlobalReference key. Objects and arrays are rejected, since a
/// semanticId value must resolve to a single scalar identifier.
/// </summary>
public sealed class SemanticIdFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "semanticId";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx) =>
        AssignExternalReference(element, resolvedValue, ctx);
}
