using MnestixCore.AasCreator.Interfaces;
using MnestixCore.AasCreator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.IdGenerator;
using MnestixCore.Shared.Interfaces;
using MnestixCore.Shared;
using MnestixCore.TemplateBuilder.Interfaces;
using MnestixCore.TemplateBuilder;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.RepoProxyClient;
using MnestixApi.Authentication;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RequiredShellsAssertion.Interfaces;
using MnestixCore.RequiredShellsAssertion;
using MnestixCore.ConfigurationService.Interfaces;
using MnestixCore.ConfigurationService;
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

            builder.Services.Configure<ConfigurationOptions>(builder.Configuration.GetSection(ConfigurationOptions.Configuration));

            builder.Services.Configure<RepositoryOpenIdConfiguration>(
                builder.Configuration.GetSection(RepositoryOpenIdConfiguration.Options));

            builder.Services.AddLogging(builder => builder.AddConsole());

            // in some classes we need the base url of the request
            builder.Services.AddHttpContextAccessor();

            
            // OpenId Authentication
            builder.Services.AddAuthenticationServices(builder.Configuration);

            // Configuration of authorization via ApiKey for endpoints used by customers
            builder.Services.Configure<CustomerEndpointsSecurityOptions>(
                builder.Configuration.GetSection(CustomerEndpointsSecurityOptions.CustomerEndpointsSecurity));

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

            builder.Services.Configure<RepoProxyOptions>(builder.Configuration.GetSection(RepoProxyOptions.RepoProxy));
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
            
            builder.Services.AddTransient<IRepoProxyClient, RepoProxyClient>();
            
            // SharedServices
            builder.Services.AddTransient<ISubmodelHandler, SubmodelHandler>();

            // IdGenerator
            builder.Services.AddTransient<IConfigurationService, ConfigurationService>();
            builder.Services.AddTransient<IAasIdGeneratorService, AasIdGeneratorService>();
            builder.Services.AddTransient<IMnestixConfigurationProvider, MnestixConfigurationProvider>();

            // AasCreator
            builder.Services.AddTransient<IAasCreatorService, AasCreatorService>();

            // TemplateBuilder
            builder.Services.AddTransient<IBlueprintCreator, BlueprintCreator>();
            builder.Services.AddTransient<ITemplateProvider, TemplateProvider>();
            builder.Services.AddTransient<IBlueprintProvider, BlueprintProvider>();
            builder.Services.AddTransient<IBlueprintValidator, BlueprintValidator>();
            builder.Services.AddTransient<ITemplateCreator, TemplateCreator>();

            // AasGenerator
            builder.Services.AddTransient<IAasGenerator, AasGenerator>();
            
            // Pipeline-based mapper
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddTransient<IDataMapper, DataMapper>();


            // Ensure mandatory shells are available in repository
            builder.Services.Configure<List<RequiredShells>>(
                builder.Configuration.GetSection(RequiredShellsOptions.RequiredShellsSectionName));
            builder.Services.AddTransient<IRequiredShellsAssertion, RequiredShellsAssertion>();

            builder.Services.AddSingleton(op =>
            {
                var baseUrlProvider = new BaseUrlProvider(op.GetService<ILogger<BaseUrlProvider>>() ??
                                                          throw new InvalidOperationException(
                                                              "ILogger must be available"));
                return baseUrlProvider;
            });

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

            // On startup the base url of the server is set once to be used in the RepoProxyClient.
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                var baseUrlProvider = app.Services.GetService<BaseUrlProvider>()
                                      ?? throw new InvalidOperationException("BaseUrlProvider must be available");
                using var scope = app.Services.CreateScope();
                var requiredShellsAssertion = scope.ServiceProvider.GetService<IRequiredShellsAssertion>() 
                                              ?? throw new InvalidOperationException("RequiredShellsAssertion must be available");
                var baseUrl = "http://localhost:5064/";
                var serverUrls = builder.Configuration.GetValue<string?>("ServerUrls");
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

                baseUrlProvider.SetBaseUrl(baseUrl);

                var parseSuccessful = bool.TryParse(builder.Configuration["Features:RequiredShells"],
                        out var requiredShells);

                if (parseSuccessful && requiredShells)
                {
                    var addExampleAasParseSuccessful = bool.TryParse(builder.Configuration["Features:AddExampleAas"],
                            out var addExampleAas);

                    requiredShellsAssertion.AssertRequiredShellsAsync(!addExampleAasParseSuccessful || addExampleAas);
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
    }
}
