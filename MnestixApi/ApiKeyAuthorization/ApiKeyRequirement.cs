using Microsoft.AspNetCore.Authorization;

namespace MnestixApi.ApiKeyAuthorization;

/// <summary>
/// Represents the requirement, that an API key must be present
/// </summary>
public class ApiKeyRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Constructor
    /// </summary>
    public ApiKeyRequirement()
    {
    }
}