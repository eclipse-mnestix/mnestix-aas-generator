using MnestixCore.Dtos.AppSettingsOptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace MnestixApi.ApiKeyAuthorization;

/// <summary>
/// Verifies that the correct API key is set.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string Apikeyname = "X-API-KEY";

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(Apikeyname, out var extractedApiKey))
        {
            context.Result = new ContentResult
            {
                StatusCode = 401,
                Content = "Api Key was not provided"
            };
            return;
        }

        var customerEndpointsSecurityOptions = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<CustomerEndpointsSecurityOptions>>();

        if (!customerEndpointsSecurityOptions.Value.ApiKey.Equals(extractedApiKey))
        {
            context.Result = new ContentResult
            {
                StatusCode = 401,
                Content = "Api Key is not valid"
            };
            return;
        }

        await next();
    }
}