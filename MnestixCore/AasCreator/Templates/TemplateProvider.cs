using System.Text.Json;
using MnestixCore.Dtos;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasCreator.Templates;

public static class TemplateProvider
{
    public static string GetAas(AasIds aasIds, AssetKind assetKind = AssetKind.Instance)
    {
        var template = EmbeddedResourceProvider.GetEmbeddedResourceContent("AasCreator.Templates.aas.json");
        return template.Replace("#assetId#", JsonEncodedText.Encode(aasIds.assetId).ToString())
            .Replace("#assetIdShort#", JsonEncodedText.Encode(aasIds.assetIdShort).ToString())
            .Replace("#aasId#", JsonEncodedText.Encode(aasIds.aasId).ToString())
            .Replace("#aasIdShort#", JsonEncodedText.Encode(aasIds.aasIdShort).ToString())
            .Replace("#assetKind#", assetKind.ToString());
    }

    public static string GetAas(AasIds aasIds, DefaultThumbnail? defaultThumbnail, AssetKind assetKind = AssetKind.Instance)
    {
        var json = GetAas(aasIds, assetKind);

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