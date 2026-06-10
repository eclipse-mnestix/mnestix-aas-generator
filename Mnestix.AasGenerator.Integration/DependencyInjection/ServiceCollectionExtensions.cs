using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.AasCreator;
using MnestixCore.AasCreator.Interfaces;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.ConfigurationService;
using MnestixCore.ConfigurationService.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.IdGenerator;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.RestClientProvider;
using MnestixCore.RestClientProvider.Interfaces;
using MnestixCore.RestClientProvider.OpenIdClientProvider;
using MnestixCore.Shared;
using MnestixCore.Shared.Interfaces;
using MnestixCore.TemplateBuilder;
using MnestixCore.TemplateBuilder.Interfaces;

namespace Mnestix.AasGenerator;

/// <summary>
/// Registers the AAS Generator engine into a consumer's <see cref="IServiceCollection"/>.
/// </summary>
public static class MnestixAasGeneratorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AAS Generator services (rules engine, blueprint provider, template builder,
    /// ID generator, repo proxy client) into the consumer's IServiceCollection. Idempotent.
    /// Validates required option values without probing repository reachability.
    /// </summary>
    public static IServiceCollection AddMnestixAasGenerator(
        this IServiceCollection services,
        Action<MnestixAasGeneratorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MnestixAasGeneratorOptions();
        configure(options);
        Validate(options);

        return AddCore(services, options);
    }

    /// <summary>
    /// Variant that binds options from an <see cref="IConfiguration"/> section.
    /// </summary>
    public static IServiceCollection AddMnestixAasGenerator(
        this IServiceCollection services,
        IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(configurationSection);

        var options = new MnestixAasGeneratorOptions();
        configurationSection.Bind(options);
        Validate(options);

        return AddCore(services, options);
    }

    private static IServiceCollection AddCore(IServiceCollection services, MnestixAasGeneratorOptions options)
    {
        // Hydrate the internal per-feature options from the public root.
        services.Configure<RepoProxyOptions>(o =>
        {
            o.AasPath = options.AasPath;
            o.SubmodelPath = options.SubmodelPath;
        });

        services.Configure<ConfigurationOptions>(o =>
        {
            o.ConfigurationSubmodelId = options.IdGenerator.ConfigurationSubmodelId;
            o.BlueprintsAasId = options.Blueprints.BlueprintsAasId;
            o.TemplatesAasId = options.Blueprints.TemplatesAasId;
            o.SubmodelBlueprintsApiUrl = options.Blueprints.BlueprintsApiUrl ?? string.Empty;
            o.SubmodelTemplatesApiUrl = options.Blueprints.TemplatesApiUrl ?? string.Empty;
        });

        services.Configure<CustomerEndpointsSecurityOptions>(o =>
        {
            o.ApiKey = options.RepositoryApiKey ?? string.Empty;
        });

        var auth = options.RepositoryAuthentication;
        // RepositoryOpenIdConfiguration has init-only members, so register a concrete instance.
        services.AddSingleton<IOptions<RepositoryOpenIdConfiguration>>(
            Options.Create(new RepositoryOpenIdConfiguration
            {
                Authority = auth?.Authority ?? string.Empty,
                DiscoveryEndpoint = auth?.DiscoveryEndpoint ?? string.Empty,
                ClientId = auth?.ClientId ?? string.Empty,
                ClientSecret = auth?.ClientSecret ?? string.Empty,
                TokenEndpoint = auth?.TokenEndpoint ?? string.Empty,
                ValidateIssuer = auth?.ValidateIssuer ?? true,
            }));

        // Repository base URL is supplied by the consumer via options, not from HttpContext.
        services.TryAddSingleton(sp =>
        {
            var provider = new BaseUrlProvider(sp.GetRequiredService<ILogger<BaseUrlProvider>>());
            provider.SetBaseUrl(options.RepositoryBaseUrl);
            return provider;
        });

        // Transport
        services.TryAddTransient<IRepoProxyClient, RepoProxyClient>();
        RegisterTransport(services, auth);

        // Shared
        services.TryAddTransient<ISubmodelHandler, SubmodelHandler>();

        // ID generation
        services.TryAddTransient<IConfigurationService, global::MnestixCore.ConfigurationService.ConfigurationService>();
        services.TryAddTransient<IAasIdGeneratorService, AasIdGeneratorService>();
        services.TryAddTransient<IMnestixConfigurationProvider, MnestixConfigurationProvider>();

        // AasCreator
        services.TryAddTransient<IAasCreatorService, AasCreatorService>();

        // TemplateBuilder (repository-backed providers/creators)
        services.TryAddTransient<IBlueprintCreator, BlueprintCreator>();
        services.TryAddTransient<ITemplateProvider, TemplateProvider>();
        services.TryAddTransient<IBlueprintProvider, BlueprintProvider>();
        services.TryAddTransient<ITemplateCreator, TemplateCreator>();

        // AasGenerator orchestrator (fetch -> map via Core engine -> persist)
        services.TryAddTransient<IAasGenerator, global::MnestixCore.AasGenerator.AasGenerator>();

        // Pure generation engine + its dependencies (IBlueprintValidator, IDataMapper, IAasGenerationEngine).
        services.AddMnestixAasGenerationCore();

        return services;
    }

    private static void RegisterTransport(IServiceCollection services, RepositoryAuthenticationOptions? auth)
    {
        if (auth?.EnableOpenIdAuth == true)
        {
            services.TryAddTransient<IAccessTokenService, AccessTokenService>();
            services.TryAddScoped<IHttpClientProvider, HttpClientTokenProvider>();
        }
        else
        {
            services.TryAddScoped<IHttpClientProvider, HttpClientProvider>();
        }
    }

    private static void Validate(MnestixAasGeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RepositoryBaseUrl)
            || !Uri.TryCreate(options.RepositoryBaseUrl, UriKind.Absolute, out _))
        {
            throw new OptionsValidationException(
                nameof(MnestixAasGeneratorOptions),
                typeof(MnestixAasGeneratorOptions),
                new[] { $"{nameof(MnestixAasGeneratorOptions.RepositoryBaseUrl)} must be a non-empty absolute URI." });
        }

        if (string.IsNullOrWhiteSpace(options.AasPath))
        {
            throw new OptionsValidationException(
                nameof(MnestixAasGeneratorOptions),
                typeof(MnestixAasGeneratorOptions),
                new[] { $"{nameof(MnestixAasGeneratorOptions.AasPath)} must be a non-empty relative path." });
        }

        if (string.IsNullOrWhiteSpace(options.SubmodelPath))
        {
            throw new OptionsValidationException(
                nameof(MnestixAasGeneratorOptions),
                typeof(MnestixAasGeneratorOptions),
                new[] { $"{nameof(MnestixAasGeneratorOptions.SubmodelPath)} must be a non-empty relative path." });
        }

        var auth = options.RepositoryAuthentication;
        if (auth?.EnableOpenIdAuth == true)
        {
            if (string.IsNullOrWhiteSpace(auth.ClientId) || string.IsNullOrWhiteSpace(auth.ClientSecret))
            {
                throw new OptionsValidationException(
                    nameof(RepositoryAuthenticationOptions),
                    typeof(RepositoryAuthenticationOptions),
                    new[] { "OpenID client-credentials flow requires ClientId and ClientSecret." });
            }

            if (string.IsNullOrWhiteSpace(auth.TokenEndpoint) && string.IsNullOrWhiteSpace(auth.Authority))
            {
                throw new OptionsValidationException(
                    nameof(RepositoryAuthenticationOptions),
                    typeof(RepositoryAuthenticationOptions),
                    new[] { "OpenID auth requires either an Authority (for discovery) or a TokenEndpoint." });
            }
        }
    }
}
