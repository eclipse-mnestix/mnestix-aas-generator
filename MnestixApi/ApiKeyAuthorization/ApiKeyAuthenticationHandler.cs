using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MnestixApi.Options;

namespace MnestixApi.ApiKeyAuthorization;

/// <summary>
/// Authenticates requests based on an API key provided via the <c>X-API-KEY</c> header.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-API-KEY";
    private readonly CustomerEndpointsSecurityOptions _securityOptions;

    /// <inheritdoc />
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<CustomerEndpointsSecurityOptions> securityOptions)
        : base(options, logger, encoder)
    {
        _securityOptions = securityOptions.Value ??
                           throw new ArgumentNullException(nameof(securityOptions));
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method))
        {
            return Task.FromResult(CreateSuccessResult());
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key header missing."));
        }

        if (string.IsNullOrWhiteSpace(_securityOptions.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is not configured."));
        }

        var providedApiKeyString = providedApiKey.ToString();

        if (!string.Equals(providedApiKeyString, _securityOptions.ApiKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is invalid."));
        }

        return Task.FromResult(CreateSuccessResult());
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append("WWW-Authenticate", Scheme.Name);
        return Task.CompletedTask;
    }

    private AuthenticateResult CreateSuccessResult()
    {
        var identity = new ClaimsIdentity(authenticationType: Scheme.Name);
        // Ensure authorization policies requiring admin.write scope succeed when authenticated via API key.
        identity.AddClaim(new Claim("scp", "admin.write"));
        identity.AddClaim(new Claim("http://schemas.microsoft.com/identity/claims/scope", "admin.write"));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
