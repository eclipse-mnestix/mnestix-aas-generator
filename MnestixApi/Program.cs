using Mnestix.AasGenerator;
using Mnestix.AasGenerator.DefaultTemplates;
using MnestixApi.Authentication;
using MnestixApi.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RequiredShellsAssertion.Interfaces;
using MnestixApi.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using MnestixApi.Extensions;
using Microsoft.AspNetCore.Rewrite;
using MnestixApi.HealthChecks;

namespace MnestixApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            // Repository base URL is derived from the host's configured server urls and passed to
            // the AAS Generator package at registration time (the package no longer reads HttpContext).
            var repositoryBaseUrl = ResolveRepositoryBaseUrl(builder.Configuration["ServerUrls"]);

            builder.Services.AddLogging(builder => builder.AddConsole());

            // in some classes we need the base url of the request
            builder.Services.AddHttpContextAccessor();

            
            // OpenId Authentication
            builder.Services.AddAuthenticationServices(builder.Configuration);

            // Configuration of authorization via ApiKey for endpoints used by customers (inbound, host-local)
            builder.Services.Configure<MnestixApi.Options.CustomerEndpointsSecurityOptions>(
                builder.Configuration.GetSection(MnestixApi.Options.CustomerEndpointsSecurityOptions.CustomerEndpointsSecurity));

            // Host-local view of the repository configuration used by controllers for routing decisions.
            builder.Services.Configure<MnestixApi.Options.ConfigurationOptions>(
                builder.Configuration.GetSection(MnestixApi.Options.ConfigurationOptions.Configuration));

            builder.Services.AddAuthorization();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("allowAnything", corsPolicyOptions =>
                {
                    corsPolicyOptions.AllowAnyOrigin();
                    corsPolicyOptions.AllowAnyHeader();
                    corsPolicyOptions.AllowAnyMethod();
                });
            });

            builder.Services.AddControllersWithViews().AddNewtonsoftJson();
            builder.Services.AddResponseCaching();

            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                // Info for clients about the supported and deprecated API versions via header
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            });
            builder.Services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV"; // v1, v2, etc.
                options.SubstituteApiVersionInUrl = true;
            });

            // NSwag doesn't support multiple api versions out of the box. That's why we need to add a document for each version.
            builder.Services.AddOpenApiDocuments();
            
            // Health Checks - Register the health check system with our custom application info check
            builder.Services.AddHealthChecks()
                .AddCheck<ApplicationInfoHealthCheck>("application_info");
            
            // Register the AAS Generator engine via the reusable package entry point.
            // The package owns all core service registrations (repo proxy client, providers,
            // id generator, rules engine) and the conditional repository transport selection.
            builder.Services.AddMnestixAasGenerator(options =>
            {
                options.RepositoryBaseUrl = repositoryBaseUrl;
                options.AasPath = builder.Configuration["RepoProxy:AasPath"] ?? "shells";
                options.SubmodelPath = builder.Configuration["RepoProxy:SubmodelPath"] ?? "submodels";
                options.RepositoryApiKey = builder.Configuration["CustomerEndpointsSecurity:ApiKey"];

                options.Blueprints.BlueprintsAasId = builder.Configuration["Configuration:BlueprintsAasId"] ?? string.Empty;
                options.Blueprints.TemplatesAasId = builder.Configuration["Configuration:TemplatesAasId"] ?? string.Empty;
                options.Blueprints.BlueprintsApiUrl = builder.Configuration["Configuration:SubmodelBlueprintsApiUrl"];
                options.Blueprints.TemplatesApiUrl = builder.Configuration["Configuration:SubmodelTemplatesApiUrl"];

                options.IdGenerator.ConfigurationSubmodelId = builder.Configuration["Configuration:ConfigurationSubmodelId"] ?? string.Empty;

                options.RepositoryAuthentication = new RepositoryAuthenticationOptions
                {
                    EnableOpenIdAuth = builder.Configuration.GetValue<bool>("RepositoryOpenIdConnect:EnableRepositoryOpenIdAuth"),
                    Authority = builder.Configuration["RepositoryOpenIdConnect:Authority"],
                    DiscoveryEndpoint = builder.Configuration["RepositoryOpenIdConnect:DiscoveryEndpoint"] ?? ".well-known/openid-configuration",
                    ClientId = builder.Configuration["RepositoryOpenIdConnect:ClientId"],
                    ClientSecret = builder.Configuration["RepositoryOpenIdConnect:ClientSecret"],
                    TokenEndpoint = builder.Configuration["RepositoryOpenIdConnect:TokenEndpoint"],
                    ValidateIssuer = builder.Configuration.GetValue<bool>("RepositoryOpenIdConnect:ValidateIssuer"),
                };
            });

            // Optional bundled IDTA/Mnestix default templates and IRequiredShellsAssertion.
            builder.Services.AddMnestixDefaultTemplates();

            // Ensure mandatory shells are available in repository (host-configured catalogue).
            builder.Services.Configure<List<RequiredShells>>(
                builder.Configuration.GetSection(RequiredShellsOptions.RequiredShellsSectionName));

            // configure app pipeline
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseCors();
            }
            
            app.UseOpenApi();
            app.UseSwaggerUi();

            app.UseHttpsRedirection();

            // Redirects calls to "api/..." to "api/v1/..." if no version is provided
            // Exceptions: /api/health and /healthz are not redirected (health check endpoints)
            var rewriteOptions = new RewriteOptions()
                .AddRedirect(@"^api$", "api/v1", statusCode: StatusCodes.Status308PermanentRedirect)
                .AddRedirect(@"^api/(?!v\d+/|health)(.*)", "api/v1/$1", statusCode: StatusCodes.Status308PermanentRedirect);
            app.UseRewriter(rewriteOptions);

            app.UseRouting();
            app.UseCors("allowAnything");

            // Configure authentication and authorization
            var authenticationParseSuccessful = bool.TryParse(builder.Configuration["Features:UseAuthentication"], out var useAuthentication);
            if (authenticationParseSuccessful && useAuthentication)
            {
                app.UseAuthentication();
            }
            else
            {
                app.Use(DefaultAuthenticationMiddleware.ConfigureDefaultAuthenticationHandling());
            }


            app.UseResponseCaching();
            app.UseAuthorization();

            // On startup, optionally seed the required default shells into the repository.
            // The repository base url is supplied to the package at registration time, so no
            // base-url assignment is needed here.
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                using var scope = app.Services.CreateScope();
                var requiredShellsAssertion = scope.ServiceProvider.GetService<IRequiredShellsAssertion>()
                                              ?? throw new InvalidOperationException("RequiredShellsAssertion must be available");

                var parseSuccessful = bool.TryParse(builder.Configuration["Features:RequiredShells"],
                        out var requiredShells);

                if (parseSuccessful && requiredShells)
                {
                    requiredShellsAssertion.AssertRequiredShellsAsync();
                }
            });

            // Map detailed health check endpoint with custom JSON response writer
            // Returns: { "status": "Healthy", "entries": {...}, "totalDuration": "..." }
            // Use this for: API consumers, monitoring dashboards, detailed diagnostics
            app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                ResponseWriter = HealthCheckResponseWriter.WriteResponse
            });

            // Map simple health check endpoint with default plain-text response
            // Returns: "Healthy" (plain text)
            // Use this for: Docker HEALTHCHECK, Kubernetes liveness/readiness probes, load balancers
            app.MapHealthChecks("/healthz");

            app.MapControllers();

            app.Run();
        }

        /// <summary>
        /// Derives the repository base URL from the host's configured server urls.
        /// Prefers an https url, falls back to the first configured url, and finally
        /// to the local default. Mirrors the previous ApplicationStarted behavior.
        /// </summary>
        private static string ResolveRepositoryBaseUrl(string? serverUrls)
        {
            var baseUrl = "http://localhost:5064/";
            if (serverUrls != null)
            {
                var serverUrlsList = serverUrls.Split(";");
                var serverUrl = serverUrlsList.FirstOrDefault(s => s.StartsWith("https:"));
                if (serverUrl == null)
                {
                    serverUrl = serverUrlsList.FirstOrDefault();
                    if (serverUrl != null)
                    {
                        baseUrl = serverUrl;
                    }
                }
                else
                {
                    baseUrl = serverUrl;
                }
            }

            return baseUrl;
        }
    }
}
