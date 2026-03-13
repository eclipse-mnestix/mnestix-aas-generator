using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines;

public sealed class DataMappingContext
{
    // Immutable inputs
    public JObject Blueprint { get; }
    public JObject Data { get; }
    public string Language { get; }
    public string NewSubmodelId { get; }
    
    // Logger for diagnostics
    private readonly ILogger<DataMappingContext> _logger;

    // Mutable working object
    public JObject SubmodelInstance { get; set; }

    public void Log(string message)
    {
        LogInfo(message);
    }
    
    public void LogInfo(string message)
    {
        Logs.Add($"INFO [{DateTime.UtcNow}] - {message}");
        _logger.LogInformation(message);
    }
    
    public void LogWarning(string message)
    {
        Logs.Add($"WARNING [{DateTime.UtcNow}] - {message}");
        _logger.LogWarning(message);
    }
    
    // Optional: diagnostics/logs for each step
    public IList<string> Logs { get; } = new List<string>();

    // Optional: The currently processed qualifiers (for error reporting)
    public JToken Qualifier { get; set; } = new JObject();

    public DataMappingContext(JObject blueprint, JObject data, string language, string newSubmodelId, ILogger<DataMappingContext> logger)
    {
        Blueprint = blueprint;
        Data = data;
        Language = language;
        NewSubmodelId = newSubmodelId;
        _logger = logger;
        SubmodelInstance = new JObject();
    }
}
