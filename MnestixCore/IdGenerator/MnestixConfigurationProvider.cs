using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Dtos.Enums;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using Newtonsoft.Json.Linq;
using static System.String;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace MnestixCore.IdGenerator;

internal class MnestixConfigurationProvider : IMnestixConfigurationProvider
{
    private readonly ILogger _logger;
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly string _base64ConfigurationSmId;
    private enum ValueIdShort
    {
        Prefix,
        DynamicPart
    }

    private enum SubmodelElementIdShort
    {
        AASID,
        AasIdShort,
        AssetId,
        AssetIdShort,
        SubmodelId
    }

    public MnestixConfigurationProvider(
        IRepoProxyClient repoProxyClient,
        IOptions<RepoProxyOptions> repoProxyOptions,
        IOptions<ConfigurationOptions> configurationOptions,
        ILogger<MnestixConfigurationProvider> logger
        )
    {
        ArgumentNullException.ThrowIfNull(repoProxyOptions);
        ArgumentNullException.ThrowIfNull(configurationOptions);

        _repoProxyClient = repoProxyClient ?? throw new ArgumentNullException(nameof(repoProxyClient));
        _logger = logger;
        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _base64ConfigurationSmId = EncodeTo64(configurationOptions.Value.ConfigurationSubmodelId);
    }

    /// <inheritdoc />
    public async Task<IdGenerationSettings> GetIdGenerationSettingsAsync(CancellationToken cancellationToken = default)
    {
        var idGeneratingSettingsSubmodelJObject = await GetIdGeneratingSettingsSubmodel(cancellationToken);

        return new IdGenerationSettings(
            GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject, SubmodelElementIdShort.AASID,
                ValueIdShort.Prefix),
            GetEnumValue<AasIdDynamicPart>(GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject,
                SubmodelElementIdShort.AASID, ValueIdShort.DynamicPart)),
            GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject, SubmodelElementIdShort.AasIdShort,
                ValueIdShort.Prefix),
            GetEnumValue<AasIdShortDynamicPart>(GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject,
                SubmodelElementIdShort.AasIdShort, ValueIdShort.DynamicPart)),
            GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject, SubmodelElementIdShort.AssetId,
                ValueIdShort.Prefix),
            GetEnumValue<AssetIdDynamicPart>(GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject,
                SubmodelElementIdShort.AssetId, ValueIdShort.DynamicPart)),
            GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject, SubmodelElementIdShort.AssetIdShort,
                ValueIdShort.Prefix),
            GetEnumValue<AssetIdShortDynamicPart>(GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject,
                SubmodelElementIdShort.AssetIdShort,
                ValueIdShort.DynamicPart)),
            GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject, SubmodelElementIdShort.SubmodelId,
                ValueIdShort.Prefix),
            GetEnumValue<SubmodelIdDynamicPart>(GetValueFromSubmodel(idGeneratingSettingsSubmodelJObject,
                SubmodelElementIdShort.SubmodelId,
                ValueIdShort.DynamicPart)));
    }

    private T GetEnumValue<T>(string valueFromSubmodel)
    {
        var result = (T)default!;
        try
        {
            result = (T)Enum.Parse(typeof(T), valueFromSubmodel, true);
        }
        catch (Exception e)
        {
            _logger.LogError($@"Could not parse value '{valueFromSubmodel}' to type '{typeof(T)}'. Use default '{result}' instead.", e);
        }

        return result;
    }

    private async Task<JObject> GetIdGeneratingSettingsSubmodel(CancellationToken cancellationToken)
    {
        var idGeneratingSettingsSubmodel =
            await _repoProxyClient.GetAsync($"{_repoProxyOptions.SubmodelPath}/{_base64ConfigurationSmId}", cancellationToken);
        var submodel = JObject.Parse(idGeneratingSettingsSubmodel!);

        return submodel;
    }

    private static string GetValueFromSubmodel(JObject submodel, SubmodelElementIdShort submodelElementIdShort,
        ValueIdShort valueIdShort)
    {
        var submodelElementsToken = submodel["submodelElements"];
        if (submodelElementsToken == null) return Empty;

        foreach (var token in submodelElementsToken)
        {
            if (!string.Equals(token["idShort"]?.ToString(), submodelElementIdShort.ToString(),
                    StringComparison.CurrentCultureIgnoreCase)) continue;

            var valueTokens = token["value"];
            if (valueTokens == null) continue;

            foreach (var valueToken in valueTokens)
            {
                if (string.Equals(valueToken["idShort"]?.ToString(), valueIdShort.ToString(),
                        StringComparison.CurrentCultureIgnoreCase))
                {
                    return valueToken["value"]?.ToString() ?? Empty;
                }
            }
        }

        return Empty;
    }
}