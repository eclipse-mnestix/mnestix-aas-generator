using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MnestixApi.HealthChecks;

/// <summary>
/// Health check that reports application version, API version, and build information.
/// </summary>
public class ApplicationInfoHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IApiVersionDescriptionProvider _apiVersionProvider;
    private readonly ILogger<ApplicationInfoHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationInfoHealthCheck"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="apiVersionProvider">The API version description provider.</param>
    /// <param name="logger">The logger.</param>
    public ApplicationInfoHealthCheck(
        IConfiguration configuration,
        IApiVersionDescriptionProvider apiVersionProvider,
        ILogger<ApplicationInfoHealthCheck> logger)
    {
        _configuration = configuration;
        _apiVersionProvider = apiVersionProvider;
        _logger = logger;
    }

    /// <summary>
    /// Checks the health of the application and returns version information.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the health check result with version data.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogTrace("Health check invoked");

            // Get the application version from assembly metadata
            var fullVersion = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "Unknown";
            
            // Clean version: strip Git hash (everything after '+')
            var appVersion = fullVersion.Split('+')[0];

            // Get the latest/highest API version from the ApiVersionDescriptionProvider
            var apiVersion = _apiVersionProvider.ApiVersionDescriptions
                .Select(v => v.ApiVersion.ToString())
                .OrderByDescending(v => v)
                .FirstOrDefault() ?? "Unknown";

            // Get build date from configuration or use current UTC time
            var buildDate = _configuration["BuildDate"] ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Create data dictionary with all version information
            var data = new Dictionary<string, object>
            {
                { "applicationVersion", appVersion },
                { "apiVersion", apiVersion },
                { "buildDate", buildDate }
            };

            // Return healthy status with version data
            return Task.FromResult(
                HealthCheckResult.Healthy(
                    description: "Application is healthy",
                    data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    description: "Health check failed",
                    exception: ex));
        }
    }
}
