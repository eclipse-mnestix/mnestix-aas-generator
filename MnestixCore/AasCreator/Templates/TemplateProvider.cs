using System.Text.Json;
using MnestixCore.Dtos;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasCreator.Templates;

public static class TemplateProvider
{
    /// <summary>
    /// Helper method to create a name-value JObject pair.
    /// Used for extensions and specificAssetIds to avoid code duplication.
    /// </summary>
    private static JObject CreateNameValueObject(string name, string value)
    {
        return new JObject
        {
            ["name"] = name,
            ["value"] = value
        };
    }

    public static string GetAas(AasIds aasIds, AssetKind assetKind = AssetKind.Instance, Dictionary<string, string>? extensions = null, List<SpecificAssetId>? specificAssetIds = null, AdministrativeInformation? administration = null)
    {
        var template = EmbeddedResourceProvider.GetEmbeddedResourceContent("AasCreator.Templates.aas.json");
        var json = template.Replace("#assetId#", JsonEncodedText.Encode(aasIds.assetId).ToString())
            .Replace("#assetIdShort#", JsonEncodedText.Encode(aasIds.assetIdShort).ToString())
            .Replace("#aasId#", JsonEncodedText.Encode(aasIds.aasId).ToString())
            .Replace("#aasIdShort#", JsonEncodedText.Encode(aasIds.aasIdShort).ToString())
            .Replace("#assetKind#", assetKind.ToString());

        if (extensions == null && specificAssetIds == null && administration == null)
        {
            return json;
        }

        var aasObject = JObject.Parse(json);

        if (extensions != null && extensions.Count > 0)
        {
            var extensionsArray = new JArray();
            foreach (var (key, value) in extensions)
            {
                extensionsArray.Add(CreateNameValueObject(key, value));
            }
            aasObject["extensions"] = extensionsArray;
        }

        if (specificAssetIds != null && specificAssetIds.Count > 0)
        {
            var assetInformation = (JObject)aasObject["assetInformation"]!;
            var currentSpecificAssetIds = (JArray)assetInformation["specificAssetIds"]!;

            foreach (var specificAssetId in specificAssetIds)
            {
                currentSpecificAssetIds.Add(CreateNameValueObject(specificAssetId.Name, specificAssetId.Value));
            }
        }

        if (administration != null)
        {
            var administrationObject = new JObject
            {
                ["version"] = administration.Version
            };
            if (!string.IsNullOrEmpty(administration.Revision))
            {
                administrationObject["revision"] = administration.Revision;
            }
            aasObject["administration"] = administrationObject;
        }

        return aasObject.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static string GetAas(AasIds aasIds, DefaultThumbnail? defaultThumbnail, AssetKind assetKind = AssetKind.Instance, Dictionary<string, string>? extensions = null, List<SpecificAssetId>? specificAssetIds = null, AdministrativeInformation? administration = null)
    {
        var json = GetAas(aasIds, assetKind, extensions, specificAssetIds, administration);

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