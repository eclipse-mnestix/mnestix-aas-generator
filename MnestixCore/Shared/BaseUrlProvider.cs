using Microsoft.Extensions.Logging;

namespace MnestixCore.Shared;

/// <summary>
/// This class is especially used by the <see cref="RepoProxyClient"/> to get the address of the local YARP-Proxy
/// to call the repository.
/// This class is intended to be used as singleton. 
/// </summary>
public class BaseUrlProvider
{
    private readonly ILogger<BaseUrlProvider> _logger;
    private string? _baseUrl;

    public BaseUrlProvider(ILogger<BaseUrlProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the base url of the local server.
    /// E.g.: https://mydomain.de:7064/
    /// </summary>
    /// <returns>The base url with trailing slash</returns>
    /// <exception cref="InvalidOperationException">The base url must be set on startup</exception>
    public string GetBaseUrl()
    {
        return _baseUrl ?? throw new InvalidOperationException("The base url must be set on startup.");
    }


    /// <param name="baseUrl">Must contain scheme ('https'), the domain, the port and a trailing /, e.g. https://mydomain.de:7064/</param>
    public void SetBaseUrl(string baseUrl)
    {
        var baseUrlToSet = baseUrl
            .Replace("*", "localhost")
            .Replace("::", "localhost:")
            .Replace("+", "localhost");
        if (!baseUrlToSet.EndsWith("/"))
        {
            baseUrlToSet += "/";
        }

        _baseUrl = baseUrlToSet;
        _logger.LogInformation("Set baseUrl to {BaseUrl}", _baseUrl);
    }
}