using System.Text.Json;
using MnestixCore.Dtos;
using MnestixCore.Shared;

namespace MnestixCore.AasCreator.Templates;

public static class TemplateProvider
{
    public static string GetAas(AasIds aasIds)
    {
        var template = EmbeddedResourceProvider.GetEmbeddedResourceContent("AasCreator.Templates.aas.json");
        return template.Replace("#assetId#", JsonEncodedText.Encode(aasIds.assetId).ToString())
            .Replace("#assetIdShort#", JsonEncodedText.Encode(aasIds.assetIdShort).ToString())
            .Replace("#aasId#", JsonEncodedText.Encode(aasIds.aasId).ToString())
            .Replace("#aasIdShort#", JsonEncodedText.Encode(aasIds.aasIdShort).ToString());
    }
}