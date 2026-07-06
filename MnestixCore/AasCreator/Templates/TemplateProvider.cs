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

    public static string GetAas(AasIds aasIds, AasCreationOptions? options = null)
    {
        var assetKind = options?.AssetKind ?? AssetKind.Instance;
        var template = EmbeddedResourceProvider.GetEmbeddedResourceContent("AasCreator.Templates.aas.json");
        var json = template.Replace("#assetId#", JsonEncodedText.Encode(aasIds.assetId).ToString())
            .Replace("#assetIdShort#", JsonEncodedText.Encode(aasIds.assetIdShort).ToString())
            .Replace("#aasId#", JsonEncodedText.Encode(aasIds.aasId).ToString())
            .Replace("#aasIdShort#", JsonEncodedText.Encode(aasIds.aasIdShort).ToString())
            .Replace("#assetKind#", assetKind.ToString());

        if (options == null || (options.Extensions == null && options.SpecificAssetIds == null && options.Administration == null && options.DerivedFrom == null && options.DefaultThumbnail == null))
        {
            return json;
        }

        var aasObject = JObject.Parse(json);

        if (options.Extensions != null && options.Extensions.Count > 0)
        {
            var extensionsArray = new JArray();
            foreach (var (key, value) in options.Extensions)
            {
                extensionsArray.Add(CreateNameValueObject(key, value));
            }
            aasObject["extensions"] = extensionsArray;
        }

        if (options.SpecificAssetIds != null && options.SpecificAssetIds.Count > 0)
        {
            var assetInformation = (JObject)aasObject["assetInformation"]!;
            var currentSpecificAssetIds = (JArray)assetInformation["specificAssetIds"]!;

            foreach (var specificAssetId in options.SpecificAssetIds)
            {
                currentSpecificAssetIds.Add(CreateNameValueObject(specificAssetId.Name, specificAssetId.Value));
            }
        }

        if (options.Administration != null)
        {
            var administrationObject = new JObject
            {
                ["version"] = options.Administration.Version
            };
            if (!string.IsNullOrEmpty(options.Administration.Revision))
            {
                administrationObject["revision"] = options.Administration.Revision;
            }
            aasObject["administration"] = administrationObject;
        }

        if (!string.IsNullOrWhiteSpace(options.DerivedFrom))
        {
            var derivedFromObject = new JObject
            {
                ["type"] = "ModelReference",
                ["keys"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "AssetAdministrationShell",
                        ["value"] = options.DerivedFrom
                    }
                }
            };
            aasObject["derivedFrom"] = derivedFromObject;
        }

        if (options.DefaultThumbnail != null)
        {
            var assetInformation = (JObject)aasObject["assetInformation"]!;
            var thumbnailObject = new JObject { ["path"] = options.DefaultThumbnail.Path };
            if (!string.IsNullOrEmpty(options.DefaultThumbnail.ContentType))
            {
                thumbnailObject["contentType"] = options.DefaultThumbnail.ContentType;
            }
            assetInformation["defaultThumbnail"] = thumbnailObject;
        }

        return aasObject.ToString(Newtonsoft.Json.Formatting.None);
    }
}