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
public class ResolveMappingExpressionsStepTests
{
    private static DataMappingContext MakeContext(JObject data, AasGeneratorOptions? options = null)
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
            options: options);
    }

    private static MappingDescriptor MakeDescriptor(string expression, string fieldName = "value")
    {
        var element = new JObject { ["modelType"] = "Property", ["idShort"] = "SerialNumber" };
        var qualifier = new JObject
        {
            ["type"] = "MnestixAASGenerator/MappingInfo/" + fieldName,
            ["value"] = expression
        };
        return new MappingDescriptor
        {
            Element = element,
            FieldName = fieldName,
            MappingExpression = expression,
            IsMandatory = false,
            ModelType = "Property",
            Qualifier = qualifier
        };
    }

    [Test]
    public async Task Execute_SimpleExpression_ResolvesValue()
    {
        var ctx = MakeContext(JObject.Parse("""{ "serialNumber": "SN-42" }"""));
        ctx.MappingDescriptors.Add(MakeDescriptor("$.serialNumber"));

        await new ResolveMappingExpressionsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        ctx.ResolvedMappings.Should().HaveCount(1);
        ctx.ResolvedMappings[0].ResolvedValue!.Value<string>().Should().Be("SN-42");
    }

    [Test]
    public async Task Execute_EvaluationTimeoutOfZeroSeconds_FailsMappingImmediately()
    {
        // Proves the step reads the timeout from AasGeneratorOptions: a hardcoded
        // timeout would let this expression finish in well under a second.
        var ctx = MakeContext(JObject.Parse("{}"), new AasGeneratorOptions { JsonataEvaluationTimeoutSeconds = 0 });
        ctx.MappingDescriptors.Add(MakeDescriptor("$count([0..2000000])"));

        Func<Task> act = () => new ResolveMappingExpressionsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        await act.Should().ThrowAsync<SubmodelDataToInstanceMapperException>()
            .WithMessage("*timeout*");
    }
}
