using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using MnestixApi.HealthChecks;

namespace Web.Tests.HealthChecks;

public class ApplicationInfoHealthCheckTests
{
    [Test]
    public async Task CheckHealthAsync_ReturnsHealthyResultWithVersionData()
    {
        // ARRANGE
        var expectedBuildDate = "2024-08-15T12:30:00Z";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BuildDate"] = expectedBuildDate
            })
            .Build();

        var versionDescriptions = new[]
        {
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", deprecated: false),
            new ApiVersionDescription(new ApiVersion(2, 1), "v2.1", deprecated: false)
        };

        var providerMock = new Mock<IApiVersionDescriptionProvider>();
        providerMock.Setup(p => p.ApiVersionDescriptions).Returns(versionDescriptions);

        var loggerMock = new Mock<ILogger<ApplicationInfoHealthCheck>>();
        var healthCheck = new ApplicationInfoHealthCheck(configuration, providerMock.Object, loggerMock.Object);

        // ACT
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // ASSERT
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Application is healthy");

        var expectedFullVersion = typeof(ApplicationInfoHealthCheck).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";
        var expectedCleanVersion = expectedFullVersion.Split('+')[0];

        result.Data.Should().ContainKey("applicationVersion")
            .WhoseValue.Should().Be(expectedCleanVersion);
        result.Data.Should().ContainKey("apiVersion")
            .WhoseValue.Should().Be("2.1");
        result.Data.Should().ContainKey("buildDate")
            .WhoseValue.Should().Be(expectedBuildDate);
    }

    [Test]
    public async Task CheckHealthAsync_WithoutBuildDate_UsesUtcNow()
    {
        // ARRANGE
        var configuration = new ConfigurationBuilder().Build();

        var versionDescriptions = Array.Empty<ApiVersionDescription>();
        var providerMock = new Mock<IApiVersionDescriptionProvider>();
        providerMock.Setup(p => p.ApiVersionDescriptions).Returns(versionDescriptions);

        var loggerMock = new Mock<ILogger<ApplicationInfoHealthCheck>>();
        var healthCheck = new ApplicationInfoHealthCheck(configuration, providerMock.Object, loggerMock.Object);

        // ACT
        var before = DateTime.UtcNow;
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        var after = DateTime.UtcNow;

        // ASSERT
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("apiVersion")
            .WhoseValue.Should().Be("Unknown");

        result.Data.Should().ContainKey("buildDate");
        var buildDate = result.Data["buildDate"].ToString();
        buildDate.Should().NotBeNull();

        var parsed = DateTime.Parse(buildDate!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        parsed.Should().BeOnOrAfter(before.AddSeconds(-1));
        parsed.Should().BeOnOrBefore(after.AddSeconds(1));
    }

    [Test]
    public async Task CheckHealthAsync_WhenProviderThrows_ReturnsUnhealthyResult()
    {
        // ARRANGE
        var configuration = new ConfigurationBuilder().Build();

        var providerMock = new Mock<IApiVersionDescriptionProvider>();
        providerMock.Setup(p => p.ApiVersionDescriptions)
            .Throws(new InvalidOperationException("Provider error"));

        var loggerMock = new Mock<ILogger<ApplicationInfoHealthCheck>>();
        var healthCheck = new ApplicationInfoHealthCheck(configuration, providerMock.Object, loggerMock.Object);

        // ACT
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // ASSERT
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Health check failed");
        result.Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Provider error");
    }
}
