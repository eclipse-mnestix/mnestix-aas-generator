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
public class NormalizeQualifierPrefixStepTests
{
    private static DataMappingContext MakeContext(JObject instance)
    {
        var logger = new WorkflowLogger(Mock.Of<ILogger>());
        var ctx = new DataMappingContext(
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
        return ctx;
    }

    [Test]
    public async Task Execute_RewritesLegacyMappingQualifiers_LeavesCardinalityAndCustom()
    {
        var instance = JObject.Parse("""
            {
              "qualifiers": [
                { "type": "SMT/MappingInfo/value", "value": "x" },
                { "type": "SMT/CollectionMappingInfo", "value": "a.b[*]" },
                { "type": "SMT/FilterMappingInfo", "value": "true" },
                { "type": "SMT/Cardinality", "value": "One" },
                { "type": "Vendor/Custom", "value": "y" }
              ]
            }
            """);
        var ctx = MakeContext(instance);

        await new NormalizeQualifierPrefixAasGeneratorPipelineStep().ExecuteAsync(ctx);

        var types = ((JArray)ctx.SubmodelInstance["qualifiers"]!)
            .Select(q => q["type"]!.Value<string>()).ToList();
        types.Should().Equal(
            "MnestixAASGenerator/MappingInfo/value",
            "MnestixAASGenerator/CollectionMappingInfo",
            "MnestixAASGenerator/FilterMappingInfo",
            "SMT/Cardinality",   // IDTA standard, unchanged
            "Vendor/Custom");    // custom, unchanged
    }

    [Test]
    public async Task Execute_NestedQualifiers_AreAlsoRewritten()
    {
        var instance = JObject.Parse("""
            {
              "submodelElements": [
                { "qualifiers": [ { "type": "SMT/MappingInfo", "value": "x" } ] }
              ]
            }
            """);
        var ctx = MakeContext(instance);

        await new NormalizeQualifierPrefixAasGeneratorPipelineStep().ExecuteAsync(ctx);

        ctx.SubmodelInstance.SelectToken("submodelElements[0].qualifiers[0].type")!
            .Value<string>().Should().Be("MnestixAASGenerator/MappingInfo");
    }

    [Test]
    public async Task Execute_LegacyPrefixPresent_LogsBackwardCompatNoticeOnce()
    {
        var instance = JObject.Parse("""
            {
              "qualifiers": [
                { "type": "SMT/MappingInfo/value", "value": "x" },
                { "type": "SMT/CollectionMappingInfo", "value": "a[*]" }
              ]
            }
            """);
        var ctx = MakeContext(instance);

        await new NormalizeQualifierPrefixAasGeneratorPipelineStep().ExecuteAsync(ctx);

        ctx.Logs.Count(l => l.Contains("backward compatibility")).Should().Be(1);
    }

    [Test]
    public async Task Execute_NoLegacyPrefix_LogsNoBackwardCompatNotice()
    {
        var instance = JObject.Parse("""
            {
              "qualifiers": [
                { "type": "MnestixAASGenerator/MappingInfo/value", "value": "x" },
                { "type": "SMT/Cardinality", "value": "One" }
              ]
            }
            """);
        var ctx = MakeContext(instance);

        await new NormalizeQualifierPrefixAasGeneratorPipelineStep().ExecuteAsync(ctx);

        ctx.Logs.Should().NotContain(l => l.Contains("backward compatibility"));
    }
}
