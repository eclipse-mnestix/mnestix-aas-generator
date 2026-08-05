using FluentAssertions;
using Microsoft.Extensions.Logging;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.Steps;
using MnestixCore.TemplateBuilder;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

[TestFixture]
public class RemoveQualifiersStepTests
{
    private static DataMappingContext MakeContext(JObject instance)
    {
        var logger = new WorkflowLogger(Mock.Of<ILogger>());
        return new DataMappingContext(
            blueprint: new JObject(),
            data: new JObject(),
            language: "en",
            newSubmodelId: "id",
            workflowLogger: logger,
            blueprintValidator: new BlueprintValidator(),
            timeProvider: TimeProvider.System)
        {
            SubmodelInstance = instance
        };
    }

    [Test]
    public async Task Execute_StripsNewPrefixMappingQualifiers_KeepsCardinality()
    {
        var instance = JObject.Parse("""
            {
              "qualifiers": [],
              "submodelElements": [
                { "qualifiers": [
                    { "type": "MnestixAASGenerator/MappingInfo/value", "value": "x" },
                    { "type": "MnestixAASGenerator/CollectionMappingInfo", "value": "a[*]" },
                    { "type": "SMT/Cardinality", "value": "One" }
                ] }
              ]
            }
            """);
        var ctx = MakeContext(instance);

        await new RemoveQualifiersAasGeneratorPipelineStep().ExecuteAsync(ctx);

        var remaining = ctx.SubmodelInstance
            .SelectToken("submodelElements[0].qualifiers")!
            .Select(q => q["type"]!.Value<string>()).ToList();
        remaining.Should().Equal("SMT/Cardinality"); // only Cardinality survives
    }
}
