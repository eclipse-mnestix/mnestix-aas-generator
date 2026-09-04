using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;

namespace MnestixApi.ApiKeyAuthorization;

/// <summary>
/// Logs a critical warning at startup when the API key is empty or set to a known default, as those values are
/// not safe for a real deployment.
/// <para>
/// This intentionally always returns <see cref="ValidateOptionsResult.Success"/>. It is a warning hook, not an
/// enforcement validator: returning <see cref="ValidateOptionsResult.Fail"/> here would throw an
/// <see cref="OptionsValidationException"/> when the options are resolved and break every request, whereas the
/// desired behaviour is to keep the application running and make the unsafe configuration clearly visible.
/// </para>
/// </summary>
public class CustomerEndpointsSecurityOptionsValidation : IValidateOptions<CustomerEndpointsSecurityOptions>
{
    private const string DefaultDevApiKey = "verySecureApiKey";

    private readonly ILogger<CustomerEndpointsSecurityOptionsValidation> _logger;

    public CustomerEndpointsSecurityOptionsValidation(ILogger<CustomerEndpointsSecurityOptionsValidation> logger)
    {
        _logger = logger;
    }

    public ValidateOptionsResult Validate(string? name, CustomerEndpointsSecurityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            _logger.LogCritical(
                "CustomerEndpointsSecurity:ApiKey is not configured. All non-GET endpoints that require an API key "
                + "will be inaccessible. Set a strong value, e.g. via the CustomerEndpointsSecurity__ApiKey "
                + "environment variable, before deploying.");
        }
        else if (options.ApiKey.Equals(DefaultDevApiKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogCritical(
                "CustomerEndpointsSecurity:ApiKey is set to the publicly-known default value. "
                + "This grants anyone who knows it write access to protected endpoints. Generate your own secret "
                + "and override it via the CustomerEndpointsSecurity__ApiKey environment variable before deploying.");
        }

        return ValidateOptionsResult.Success;
    }
}