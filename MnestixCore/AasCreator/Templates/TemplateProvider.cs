using MnestixCore.Dtos;
using MnestixCore.Shared;

namespace MnestixCore.AasCreator.Templates;

public static class TemplateProvider
{
    public static string GetAas(AasIds aasIds)
    {
        var template = EmbeddedResourceProvider.GetEmbeddedResourceContent("AasCreator.Templates.aas.json");
        return template.Replace("#assetId#", aasIds.assetId)
            .Replace("#assetIdShort#", aasIds.assetIdShort)
            .Replace("#aasId#", aasIds.aasId)
            .Replace("#aasIdShort#", aasIds.aasIdShort);
    }
}