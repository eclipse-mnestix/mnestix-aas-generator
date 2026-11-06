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
public class FilterElementsStepTests
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

    private static JObject MakeInstance(string filterExpression)
    {
        return JObject.Parse($$"""
            {
              "modelType": "SubmodelElementCollection",
              "qualifiers": [],
              "value": [
                {
                  "modelType": "Property",
                  "idShort": "SerialNumber",
                  "qualifiers": [
                    { "type": "MnestixAASGenerator/FilterMappingInfo", "value": "{{filterExpression}}" }
                  ],
                  "value": "keep-me"
                }
              ]
            }
            """);
    }

    [Test]
    public async Task Execute_FilterEvaluatingFalse_RemovesElement()
    {
        var instance = MakeInstance("$.active = true");
        var data = JObject.Parse("""{ "active": false }""");
        var ctx = MakeContext(instance, data);

        await new FilterElementsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        ctx.SubmodelInstance.SelectTokens("value[*]").Should().BeEmpty();
    }

    [Test]
    public async Task Execute_FilterEvaluatingTrue_KeepsElement()
    {
        var instance = MakeInstance("$.active = true");
        var data = JObject.Parse("""{ "active": true }""");
        var ctx = MakeContext(instance, data);

        await new FilterElementsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        ctx.SubmodelInstance.SelectTokens("value[*]").Should().HaveCount(1);
    }

    [Test]
    public async Task Execute_EvaluationTimeoutOfZeroSeconds_FailsMappingImmediately()
    {
        // Proves the step reads the timeout from AasGeneratorOptions: a hardcoded
        // timeout would let this expression finish in well under a second.
        var instance = MakeInstance("$count([0..2000000]) > 0");
        var data = JObject.Parse("{}");
        var ctx = MakeContext(instance, data, new AasGeneratorOptions { JsonataEvaluationTimeoutSeconds = 0 });

        Func<Task> act = () => new FilterElementsAasGeneratorPipelineStep().ExecuteAsync(ctx);

        await act.Should().ThrowAsync<SubmodelDataToInstanceMapperException>()
            .WithMessage("*timeout*");
    }
}
