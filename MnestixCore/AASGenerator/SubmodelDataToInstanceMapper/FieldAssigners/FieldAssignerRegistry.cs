namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Registry that maps field names to their assigner instances.
/// Known special-case fields get dedicated assigners; unknown fields fall back to DefaultFieldAssigner.
/// </summary>
public static class FieldAssignerRegistry
{
    private static readonly Dictionary<string, FieldAssignerBase> KnownAssigners = new()
    {
        ["value"] = new ValueFieldAssigner(),
        ["valueType"] = new ValueTypeFieldAssigner(),
        ["semanticId"] = new SemanticIdFieldAssigner(),
        ["idShort"] = new IdShortFieldAssigner(),
        ["multiLanguage"] = new MultiLanguageFieldAssigner(),
        ["displayName"] = new DisplayNameFieldAssigner(),
        ["first"] = new FirstFieldAssigner(),
        ["second"] = new SecondFieldAssigner(),
    };

    /// <summary>
    /// Returns the appropriate field assigner for the given field name.
    /// Unknown field names get a DefaultFieldAssigner that assigns element[fieldName] = value.ToString().
    /// </summary>
    public static FieldAssignerBase GetAssigner(string fieldName)
    {
        if (KnownAssigners.TryGetValue(fieldName, out var assigner))
        {
            return assigner;
        }

        return new DefaultFieldAssigner(fieldName);
    }
}
