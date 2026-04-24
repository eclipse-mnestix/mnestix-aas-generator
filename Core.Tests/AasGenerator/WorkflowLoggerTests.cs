using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MnestixCore.AasGenerator;
using Moq;

namespace Core.Tests.AasGenerator;

public class WorkflowLoggerTests
{
    private Mock<ILogger> _loggerMock = null!;
    private WorkflowLogger _workflowLogger = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger>();
        _workflowLogger = new WorkflowLogger(_loggerMock.Object);
    }

    [Test]
    public void LogInfo_AddsFormattedEntryToLogs()
    {
        _workflowLogger.LogInfo("test message");

        _workflowLogger.Logs.Should().HaveCount(1);
        _workflowLogger.Logs[0].Should().MatchRegex(@"^INFO \[.+\] - test message$");
    }

    [Test]
    public void LogWarning_AddsFormattedEntryToLogs()
    {
        _workflowLogger.LogWarning("warning message");

        _workflowLogger.Logs.Should().HaveCount(1);
        _workflowLogger.Logs[0].Should().MatchRegex(@"^WARNING \[.+\] - warning message$");
    }

    [Test]
    public void LogError_AddsFormattedEntryToLogs()
    {
        _workflowLogger.LogError("error message");

        _workflowLogger.Logs.Should().HaveCount(1);
        _workflowLogger.Logs[0].Should().MatchRegex(@"^ERROR \[.+\] - error message$");
    }

    [Test]
    public void LogInfo_ForwardsToILogger()
    {
        _workflowLogger.LogInfo("forwarded message");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("forwarded message")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void LogWarning_ForwardsToILogger()
    {
        _workflowLogger.LogWarning("warning forwarded");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("warning forwarded")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void LogError_ForwardsToILogger()
    {
        _workflowLogger.LogError("error forwarded");

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("error forwarded")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public void AddRange_MergesExternalEntriesIntoLogs()
    {
        _workflowLogger.LogInfo("step 1");
        var externalLogs = new List<string> { "INFO [2026-01-01T00:00:00Z] - external step A", "WARNING [2026-01-01T00:00:01Z] - external step B" };

        _workflowLogger.AddRange(externalLogs);

        _workflowLogger.Logs.Should().HaveCount(3);
        _workflowLogger.Logs[1].Should().Contain("external step A");
        _workflowLogger.Logs[2].Should().Contain("external step B");
    }

    [Test]
    public void Logs_PreservesChronologicalOrder()
    {
        _workflowLogger.LogInfo("first");
        _workflowLogger.LogWarning("second");
        _workflowLogger.LogError("third");

        _workflowLogger.Logs.Should().HaveCount(3);
        _workflowLogger.Logs[0].Should().StartWith("INFO");
        _workflowLogger.Logs[1].Should().StartWith("WARNING");
        _workflowLogger.Logs[2].Should().StartWith("ERROR");
    }

    [Test]
    public void LogEntryFormat_MatchesDataMappingContextConvention()
    {
        _workflowLogger.LogInfo("test");
        _workflowLogger.LogWarning("test");
        _workflowLogger.LogError("test");

        var pattern = new Regex(@"^(INFO|WARNING|ERROR) \[.+\] - .+$");
        foreach (var log in _workflowLogger.Logs)
        {
            pattern.IsMatch(log).Should().BeTrue($"Log entry '{log}' should match the format convention");
        }
    }

    [Test]
    public void NewInstance_HasEmptyLogs()
    {
        _workflowLogger.Logs.Should().BeEmpty();
    }
}
