using System.Security.Claims;
using MnestixApi.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace MnestixApi.ApiKeyAuthorization;

/// <summary>
/// An authorization handler which requires that an API key is set for all requests (except for GET).
/// </summary>
public class ApiKeyRequirementHandler : AuthorizationHandler<ApiKeyRequirement>
{
    private const string ApiKeyHeaderName = "X-API-KEY";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CustomerEndpointsSecurityOptions _customerEndpointsSecurityOptions;

    /// <inheritdoc />
    public ApiKeyRequirementHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<CustomerEndpointsSecurityOptions> customerEndpointsSecurityOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _customerEndpointsSecurityOptions = customerEndpointsSecurityOptions.Value ??
                                            throw new ArgumentNullException(nameof(customerEndpointsSecurityOptions));
    }

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyRequirement requirement)
    {
        SucceedRequirementIfApiKeyPresentAndValid(context, requirement);
        return Task.CompletedTask;
    }

    private void SucceedRequirementIfApiKeyPresentAndValid(AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement)
    {
        if (_httpContextAccessor.HttpContext?.Request.Method is "GET" or "HEAD")
        {
            context.Succeed(requirement);
            return;
        }

        var apiKey = new StringValues();

        // This check is only valid for the /settings route to ensure the user is authenticated 
        // and is not the DefaultUser while having the "mnestix-admin" role.
        // The /template route first hits the API endpoint and then makes a call to the repository via a proxy.
        //
        // TODO: Reevaluate how authentication works after splitting the proxy from the API.
        bool isAuthenticatedNonDefaultUser =
            context.User.Identity?.IsAuthenticated == true &&
            context.User.Claims.Any(c => c is { Type: ClaimTypes.Role, Value: "mnestix-admin" }) &&
            context.User.Identity?.Name != "DefaultUser";

        _httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(ApiKeyHeaderName, out apiKey);
        if (isAuthenticatedNonDefaultUser || apiKey == _customerEndpointsSecurityOptions.ApiKey)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(this,
            "For all methods except 'GET' you need a valid X-API-KEY in your header."));
    }
}