using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines;

/// <summary>
/// Describes a single mapping qualifier discovered during validation.
/// Carries all metadata needed by downstream steps (resolve + assign).
/// </summary>
public sealed class MappingDescriptor
{
    /// <summary>The parent SME element that owns this qualifier.</summary>
    public required JToken Element { get; init; }

    /// <summary>The target field name (e.g. "value", "idShort", "globalAssetId").</summary>
    public required string FieldName { get; init; }

    /// <summary>The JSONata expression (qualifier value) to evaluate against the data payload.</summary>
    public required string MappingExpression { get; init; }

    /// <summary>Whether this mapping is mandatory (cardinality starts with "One").</summary>
    public required bool IsMandatory { get; init; }

    /// <summary>The modelType of the parent element (e.g. "Property", "Entity").</summary>
    public required string ModelType { get; init; }

    /// <summary>The original qualifier token (for error context).</summary>
    public required JToken Qualifier { get; init; }
}
