using System.Collections.Frozen;

namespace MnestixCore.Shared;

public sealed class ModelTypeMapping
{
    private readonly FrozenDictionary<string, FieldSpec> _fields;

    public ModelTypeMapping(IReadOnlyList<FieldSpec> fields)
    {
        _fields = fields.ToFrozenDictionary(f => f.FieldName);
    }

    public bool Contains(string fieldName) => _fields.ContainsKey(fieldName);
    
    public FieldSpec? Get(string fieldName) =>
        _fields.TryGetValue(fieldName, out var spec) ? spec : null;
    
    public IEnumerable<string> FieldNames => _fields.Keys;
}
