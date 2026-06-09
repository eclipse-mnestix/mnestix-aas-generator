namespace Mnestix.AasGenerator;

/// <summary>
/// Single configuration entry point a consumer supplies to <c>AddMnestixAasGenerator</c>.
/// Internally the DI extension hydrates the per-feature options classes from this root.
/// </summary>
public sealed class MnestixAasGeneratorOptions
{
    /// <summary>Base URL of the AAS / Submodel repository. Required.</summary>
    public string RepositoryBaseUrl { get; set; } = string.Empty;

    /// <summary>Relative path under the repo for AAS shells. Default: "shells".</summary>
    public string AasPath { get; set; } = "shells";

    /// <summary>Relative path under the repo for submodels. Default: "submodels".</summary>
    public string SubmodelPath { get; set; } = "submodels";

    /// <summary>
    /// Optional outbound API key for repository auth.
    /// Not to be confused with any inbound API-key middleware the host configures.
    /// </summary>
    public string? RepositoryApiKey { get; set; }

    public BlueprintSourceOptions Blueprints { get; set; } = new();

    public IdGeneratorOptions IdGenerator { get; set; } = new();

    public RepositoryAuthenticationOptions? RepositoryAuthentication { get; set; }
}

public sealed class BlueprintSourceOptions
{
    /// <summary>AAS id holding blueprint submodels (when using the BaSyx-backed default provider).</summary>
    public string BlueprintsAasId { get; set; } = string.Empty;

    /// <summary>AAS id holding template submodels (when using the BaSyx-backed default provider).</summary>
    public string TemplatesAasId { get; set; } = string.Empty;

    /// <summary>Optional override repo URL for blueprints (defaults to <see cref="MnestixAasGeneratorOptions.RepositoryBaseUrl"/>).</summary>
    public string? BlueprintsApiUrl { get; set; }

    /// <summary>Optional override repo URL for templates (defaults to <see cref="MnestixAasGeneratorOptions.RepositoryBaseUrl"/>).</summary>
    public string? TemplatesApiUrl { get; set; }
}

public sealed class IdGeneratorOptions
{
    /// <summary>Submodel id where ID-generation settings (prefixes, GUID strategy) are persisted.</summary>
    public string ConfigurationSubmodelId { get; set; } = string.Empty;
}

public sealed class RepositoryAuthenticationOptions
{
    public bool EnableOpenIdAuth { get; set; }
    public string? Authority { get; set; }
    public string DiscoveryEndpoint { get; set; } = ".well-known/openid-configuration";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool ValidateIssuer { get; set; } = true;
    public string? TokenEndpoint { get; set; }
}
