using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixApi.Authentication;

/// <summary>
/// Helper class for extracting and normalizing claims from Keycloak access tokens.
/// </summary>
public static class KeycloakClaimHelper
{
    /// <summary>
    /// Extracts role claims from the 'resource_access' section of a Keycloak access token
    /// and adds them as standard <see cref="ClaimTypes.Role"/> claims.
    /// </summary>
    /// <param name="context">The context containing the validated token and claims identity.</param>
    /// <param name="clientId">The client ID configured in Keycloak, used to locate the correct role set.</param>
    /// <remarks>
    /// This method enables the use of standard role-based authorization attributes such as
    /// <c>[Authorize(Roles = "admin")]</c> by mapping Keycloak roles to <see cref="ClaimTypes.Role"/>.
    /// 
    /// Example Keycloak claim structure:
    /// <code>
    /// "resource_access": {
    ///   "your-client-id": {
    ///     "roles": [ "admin", "editor" ]
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static void AddRolesFromResourceAccess(TokenValidatedContext context, string clientId)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity) return;

        var resourceAccessClaim = context.Principal?.FindFirst("resource_access");
        if (resourceAccessClaim == null) return;

        try
        {
            var resourceAccess = JsonConvert.DeserializeObject<JObject>(resourceAccessClaim.Value);

            if (resourceAccess?[clientId]?["roles"] is not JArray roles) return;
            foreach (var role in roles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Failed to parse resource_access: {ex.Message}");
        }
    }
}