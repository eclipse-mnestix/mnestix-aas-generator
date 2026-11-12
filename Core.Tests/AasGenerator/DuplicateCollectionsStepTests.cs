using FluentAssertions;
using Microsoft.Extensions.Logging;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.Steps;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

[TestFixture]
public class DuplicateCollectionsStepTests
{
    private static DataMappingContext MakeContext(JObject instance, JObject data, AasGeneratorOptions? options = null)
    {
        var logger = new WorkflowLogger(Mock.Of<ILogger>());
        return new DataMappingContext(
            blueprint: new JObject(),
            data: data,
            language: "en",
            newSubmodelId: "id",
            workflowLogger: logger,
            blueprintValidator: new BlueprintValidator(),
            timeProvider: TimeProvider.System,
            options: options)
        {
            SubmodelInstance = instance
        };
    }

    [Test]
    public async Task Execute_FilterMappingInfoInsideDuplicatedElement_RewritesWildcardToIndexPerDuplicate()
    {
        var instance = JObject.Parse("""
            {
              "modelType": "SubmodelElementCollection",
              "qualifiers": [],
              "value": [
                {
                  "idShort": "Vehicle",
                  "qualifiers": [
                    { "type": "MnestixAASGenerator/CollectionMappingInfo", "value": "vehicles[*]" }
                  ],
                  "value": [
                    {
                      "idShort": "BatteryInfo",
                      "qualifiers": [
                        { "type": "MnestixAASGenerator/FilterMappingInfo", "value": "vehicles[*].engineType = 'electric'" }
                      ]
                    }
                  ]
                }
              ]
            }
            """);
        var data = JObject.Parse("""
            {
              "vehicles": [
                { "engineType": "electric" },
                { "engineType": "combustion" }
              ]
            }
            """);
        var ctx = MakeContext(instance, data);

        await new DuplicateCollectionsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        var duplicates = ctx.SubmodelInstance.SelectTokens("value[*]").ToList();
        duplicates.Should().HaveCount(2);

        var filterValues = duplicates
            .Select(d => d.SelectToken("value[0].qualifiers[0].value")!.Value<string>())
            .ToList();

        // Each duplicate's FilterMappingInfo must be re-indexed to match its own duplicate ([0], [1]),
        // not left as the un-iterated "[*]" wildcard - that was the original bug.
        filterValues.Should().Equal(
            "vehicles[0].engineType = 'electric'",
            "vehicles[1].engineType = 'electric'");
    }

    [Test]
    public async Task Execute_CollectionWithMoreElementsThanConfiguredPayloadLimit_ThrowsInsteadOfDuplicating()
    {
        var instance = JObject.Parse("""
            {
              "modelType": "SubmodelElementCollection",
              "qualifiers": [],
              "value": [
                {
                  "idShort": "Vehicle",
                  "qualifiers": [
                    { "type": "MnestixAASGenerator/CollectionMappingInfo", "value": "vehicles[*]" }
                  ],
                  "value": []
                }
              ]
            }
            """);
        var data = JObject.Parse("""
            {
              "vehicles": [
                { "engineType": "electric" },
                { "engineType": "combustion" },
                { "engineType": "hybrid" }
              ]
            }
            """);
        var ctx = MakeContext(instance, data, new AasGeneratorOptions { MaxPayloadLimit = 2 });

        Func<Task> act = () => new DuplicateCollectionsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        await act.Should().ThrowAsync<SubmodelDataToInstanceMapperException>()
            .WithMessage("*payload limit*2*");
    }
}
