using MnestixCore.TemplateBuilder;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines;

internal sealed class DataMappingContext
{
    // Immutable inputs
    public JObject Blueprint { get; }
    public JObject Data { get; }
    public string? Language { get; }
    public string NewSubmodelId { get; }
    public IBlueprintValidator BlueprintValidator { get; }
    
    // Shared workflow logger
    private readonly WorkflowLogger _workflowLogger;

    // Mutable working object
    public JObject SubmodelInstance { get; set; }

    public void Log(string message)
    {
        LogInfo(message);
    }
    
    public void LogInfo(string message)
    {
        _workflowLogger.LogInfo(message);
    }
    
    public void LogWarning(string message)
    {
        _workflowLogger.LogWarning(message);
    }
    
    // Logs are stored in the shared WorkflowLogger
    public IList<string> Logs => _workflowLogger.Logs;

    // Optional: The currently processed qualifiers (for error reporting)
    public JToken Qualifier { get; set; } = new JObject();

    // Intermediate state passed between pipeline steps
    public List<MappingDescriptor> MappingDescriptors { get; set; } = new();
    public List<ResolvedMapping> ResolvedMappings { get; set; } = new();

    public DataMappingContext(JObject blueprint, JObject data, string? language, string newSubmodelId, WorkflowLogger workflowLogger, IBlueprintValidator blueprintValidator)
    {
        Blueprint = blueprint;
        Data = data;
        Language = language;
        NewSubmodelId = newSubmodelId;
        _workflowLogger = workflowLogger;
        BlueprintValidator = blueprintValidator;
        SubmodelInstance = new JObject();
    }
}
