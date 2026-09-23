using FluentAssertions;
using Microsoft.Extensions.Logging;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.Shared;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

[TestFixture]
public class JsonataEvaluatorTests
{
    private static DataMappingContext MakeContext()
    {
        var logger = new WorkflowLogger(Mock.Of<ILogger>());
        return new DataMappingContext(
            blueprint: new JObject(),
            data: new JObject(),
            language: "en",
            newSubmodelId: "id",
            workflowLogger: logger,
            blueprintValidator: new BlueprintValidator(),
            timeProvider: TimeProvider.System);
    }

    [Test]
    public async Task EvaluateAsync_ExpressionExceedingTimeout_ThrowsWithClearMessage()
    {
        var ctx = MakeContext();

        Func<Task> act = () => JsonataEvaluator.EvaluateAsync(
            JObject.Parse("{}"),
            // Takes well over half a second to evaluate; the 50ms timeout must abandon it
            "$count([0..5000000])",
            ctx,
            TimeSpan.FromMilliseconds(50));

        await act.Should().ThrowAsync<SubmodelDataToInstanceMapperException>()
            .WithMessage("*timeout*");
    }
}
