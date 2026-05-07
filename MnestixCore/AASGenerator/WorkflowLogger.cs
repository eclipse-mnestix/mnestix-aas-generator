using Microsoft.Extensions.Logging;

namespace MnestixCore.AasGenerator;

/// <summary>
/// Accumulates in-memory log entries for a single blueprint workflow while also forwarding
/// each entry to the structured <see cref="ILogger"/> infrastructure.
/// </summary>
public sealed class WorkflowLogger
{
    private readonly ILogger _logger;

    public List<string> Logs { get; } = new List<string>();

    public WorkflowLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void LogInfo(string message)
    {
        Logs.Add($"INFO [{DateTime.UtcNow:O}] - {message}");
        _logger.LogInformation(message);
    }

    public void LogWarning(string message)
    {
        Logs.Add($"WARNING [{DateTime.UtcNow:O}] - {message}");
        _logger.LogWarning(message);
    }

    public void LogError(string message)
    {
        Logs.Add($"ERROR [{DateTime.UtcNow:O}] - {message}");
        _logger.LogError(message);
    }

    public void AddRange(IEnumerable<string> entries)
    {
        Logs.AddRange(entries);
    }
}
