using NSwag;
using NSwag.Generation.Processors.Security;

namespace MnestixApi.Extensions;

/// <summary>
/// Provides extension methods for registering OpenAPI documents in an application.
/// </summary>
public static class OpenApiDocumentsRegistration
{
    /// <summary>
    /// Registers OpenAPI documents for the configured API versions.
    /// </summary>
    /// <param name="services">The service collection where the OpenAPI documents are registered.</param>
    public static void AddOpenApiDocuments(this IServiceCollection services)
    {
        // Define the different API versions here
        var versions = new[] { "v1", "v2" };

        foreach (var version in versions)
        {
            services.AddOpenApiDocument(document =>
            {
                document.DocumentName = version;
                document.ApiGroupNames = new[] { version };

                document.OperationProcessors.Add(new OperationSecurityScopeProcessor("ApiKey"));
                document.DocumentProcessors.Add(new SecurityDefinitionAppender("ApiKey", new OpenApiSecurityScheme
                {
                    Type = OpenApiSecuritySchemeType.ApiKey,
                    Name = "X-API-KEY",
                    In = OpenApiSecurityApiKeyLocation.Header,
                    Description = "X-API-KEY in the Header"
                }));

                document.PostProcess = d =>
                {
                    d.Info.Title = "Mnestix AAS Generator";
                    // Adapt manually. This is the application version, NOT the Api Version
                    d.Info.Version = "1.4.0";
                    d.Info.Contact = new OpenApiContact
                    {
                        Name = "XITASO GmbH",
                        Email = "info@xitaso.com",
                        Url = "https://www.xitaso.com"
                    };
                };
            });
        }
    }
    
}