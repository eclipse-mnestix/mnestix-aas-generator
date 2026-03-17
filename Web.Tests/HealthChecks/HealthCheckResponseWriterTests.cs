using System.Collections.ObjectModel;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MnestixApi.HealthChecks;

namespace Web.Tests.HealthChecks;

public class HealthCheckResponseWriterTests
{
    [Test]
    public async Task WriteResponse_WritesExpectedJsonStructure()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var entry = new HealthReportEntry(
            status: HealthStatus.Healthy,
            description: "All good",
            duration: TimeSpan.FromMilliseconds(150),
            exception: null,
            data: new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
            {
                ["applicationVersion"] = "1.2.3",
                ["buildDate"] = "2024-08-15T12:30:00Z"
            }),
            tags: Array.Empty<string>());

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry> { ["application"] = entry },
            TimeSpan.FromMilliseconds(200));

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.ContentType.Should().Be("application/json; charset=utf-8");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();

        var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().Be("Healthy");
        root.GetProperty("totalDuration").GetString().Should().Be("00:00:00.2000000");

        var entryElement = root.GetProperty("entries").GetProperty("application");
        entryElement.GetProperty("status").GetString().Should().Be("Healthy");
        entryElement.GetProperty("description").GetString().Should().Be("All good");
        entryElement.GetProperty("duration").GetString().Should().Be("00:00:00.1500000");

        var dataElement = entryElement.GetProperty("data");
        dataElement.GetProperty("applicationVersion").GetString().Should().Be("1.2.3");
        dataElement.GetProperty("buildDate").GetString().Should().Be("2024-08-15T12:30:00Z");
    }

    [Test]
    public async Task WriteResponse_IncludesExceptionDetailsWhenPresent()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new InvalidOperationException("boom");
        var entry = new HealthReportEntry(
            status: HealthStatus.Unhealthy,
            description: "Failure",
            duration: TimeSpan.Zero,
            exception: exception,
            data: new ReadOnlyDictionary<string, object>(new Dictionary<string, object>()),
            tags: []);

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry> { ["application"] = entry },
            TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();

        var document = JsonDocument.Parse(payload);
        var entryElement = document.RootElement
            .GetProperty("entries")
            .GetProperty("application");

        entryElement.GetProperty("status").GetString().Should().Be("Unhealthy");
        entryElement.GetProperty("exception").GetString().Should().Be("boom");
        entryElement.GetProperty("exceptionType").GetString().Should().Be(typeof(InvalidOperationException).Name);
    }
}
