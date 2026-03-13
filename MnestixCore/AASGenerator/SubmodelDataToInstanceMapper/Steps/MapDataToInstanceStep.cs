using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Jsonata.Net.Native;
using Jsonata.Net.Native.JsonNet;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

public sealed class MapDataToInstanceAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started MapDataToInstanceStep");
        MapDataToInstance(ctx);
        ctx.Log($"Finished MapDataToInstanceStep");
        return Task.FromResult(ctx);
    }

    private static JArray ConvertToMultiLanguageProperty(JToken text, string language)
    {
        // Currently only one language is supported
        // See docs/rules-engine.md -> MultiLanguageProperty for more information
        return new JArray
        {
            new JObject
            {
                {"text", text},
                {"language", language}
            }
        };
    }

    private static void AssignJsonValueToInstance(JToken blueprintValue, JToken dataFromMappingPath, JToken modelType, string language)
    {
        if (modelType.Value<string>() == "MultiLanguageProperty")
        {
            blueprintValue.Replace(ConvertToMultiLanguageProperty(dataFromMappingPath, language));
            return;
        }

        blueprintValue.Replace(dataFromMappingPath);
    }

    private static JToken? SelectTokenFromDataJson(JToken dataJson, string mappingPath, DataMappingContext ctx)
    {
        try
        {
            // JSONATA returns Undefined (JToken with Type=Undefined) for missing paths instead of null
            var query = new JsonataQuery(mappingPath);
            var result = query.EvalNewtonsoft(dataJson);
            
            // Convert JSONATA Undefined to null for backward compatibility
            if (result?.Type == JTokenType.Undefined)
            {
                return null;
            }
            
            return result;
        }
        catch (Exception e) when (!(e is SubmodelDataToInstanceMapperException))
        {
            
            throw new SubmodelDataToInstanceMapperException($"Error while evaluating JSONATA expression '{mappingPath}': " + e.Message, e, ctx);
            
        }
    }

    private static void CheckIfValueKeyExists(JToken blueprintValue)
    {
        /*
        As per the v3 standard, "value" in MultiLanguageProperty has Cardinality "0..1,"
        indicating potential absence in blueprint data. We handle this by creating
        an empty value for the key "value" during mapping, ensuring smooth mapping without exceptions.
        */
        var parent = blueprintValue.Parent?.Parent?.Parent;
        if (parent?["value"] != null) return;
        if (parent != null) parent["value"] = new JArray();
    }

    private static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.parent is the "qualifiers" array
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }

    private static void MapDataToInstance(DataMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;
        var data = ctx.Data;
        var language = ctx.Language;
        
        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[?(@.type=='SMT/MappingInfo')]");

        foreach (var qualifier in qualifiers)
        {
            ctx.Qualifier = qualifier;
            var modelType = qualifier.Parent?.Parent?.Parent?["modelType"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching modelType field of a qualify object", ctx);
            if (modelType.Value<string>() == "MultiLanguageProperty") CheckIfValueKeyExists(qualifier);
            var blueprintValue = qualifier.Parent?.Parent?.Parent?["value"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching value field of a qualify object", ctx);
            var mappingPath = qualifier["value"]?.Value<string>() ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);
            var isMandatory = GetCardinalityQualifier(qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;
            var dataFromMappingPath = SelectTokenFromDataJson(data, mappingPath, ctx);

            // If no data is found and the mapping is mandatory an error will be thrown
            if (dataFromMappingPath == null)
            {
                if (isMandatory)
                {
                    throw new SubmodelDataToInstanceMapperException($"Mandatory mapping '{mappingPath}' not found.", ctx);
                }
                else
                {
                    ctx.LogWarning($"Optional mapping '{mappingPath}' not found in data, skipping.");
                    continue;
                }
            }
            AssignJsonValueToInstance(blueprintValue, dataFromMappingPath, modelType, language);
            ctx.LogInfo($"Successfully mapped value '{dataFromMappingPath}' from path '{mappingPath}'");


        }
    }
}
