namespace MnestixCore.Shared;

public readonly record struct FieldSpec(
    string FieldName,
    FieldSpec.Cardinality FieldCardinality = FieldSpec.Cardinality.InheritsFromElement)
{
    public enum Cardinality
    {
        InheritsFromElement,
        AlwaysOptional,
        AlwaysMandatory,
    }
}
