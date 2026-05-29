using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Default field assigner for any field name that has no special handling.
/// Simply assigns element[fieldName] = resolvedValue.ToString().
/// </summary>
public sealed class DefaultFieldAssigner : FieldAssignerBase
{
    private readonly string _fieldName;

    public DefaultFieldAssigner(string fieldName)
    {
        _fieldName = fieldName;
    }

    public override string FieldName => _fieldName;
}
