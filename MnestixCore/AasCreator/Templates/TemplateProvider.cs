using MnestixCore.Dtos;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

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

    public static string GetAas(AasIds aasIds, DefaultThumbnail? defaultThumbnail)
    {
        var json = GetAas(aasIds);

        if (defaultThumbnail == null)
        {
            return json;
        }

        var aasObject = JObject.Parse(json);
        var assetInformation = (JObject)aasObject["assetInformation"]!;

        var thumbnailObject = new JObject { ["path"] = defaultThumbnail.Path };
        if (!string.IsNullOrEmpty(defaultThumbnail.ContentType))
        {
            thumbnailObject["contentType"] = defaultThumbnail.ContentType;
        }

        assetInformation["defaultThumbnail"] = thumbnailObject;

        return aasObject.ToString(Newtonsoft.Json.Formatting.None);
    }
}