using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.FieldAssigners;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

[TestFixture]
public class FieldAssignerTests
{
    private static DataMappingContext MakeContext()
    {
        return new DataMappingContext(
            new JObject(), new JObject(), null, "urn:new",
            new WorkflowLogger(NullLogger.Instance), new BlueprintValidator());
    }

    [Test]
    public void SemanticId_StringValue_WrappedAsExternalReference()
    {
        var element = new JObject { ["modelType"] = "Property", ["idShort"] = "P" };
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, JValue.CreateString("https://example.com/sid"), "Property", null, MakeContext());

        element["semanticId"]!["type"]!.Value<string>().Should().Be("ExternalReference");
        element["semanticId"]!["keys"]![0]!["type"]!.Value<string>().Should().Be("GlobalReference");
        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be("https://example.com/sid");
    }

    [Test]
    public void SemanticId_ObjectValue_AssignedAsIs()
    {
        var element = new JObject { ["modelType"] = "Property", ["idShort"] = "P" };
        var reference = new JObject { ["type"] = "ModelReference", ["keys"] = new JArray() };
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, reference, "Property", null, MakeContext());

        element["semanticId"]!["type"]!.Value<string>().Should().Be("ModelReference");
    }

    [Test]
    public void ValueType_ValidType_Assigned()
    {
        var element = new JObject { ["modelType"] = "Property", ["idShort"] = "P" };
        var sut = new ValueTypeFieldAssigner();

        sut.Assign(element, JValue.CreateString("xs:integer"), "Property", null, MakeContext());

        element["valueType"]!.Value<string>().Should().Be("xs:integer");
    }

    [Test]
    public void ValueType_InvalidType_Throws()
    {
        var element = new JObject { ["modelType"] = "Property", ["idShort"] = "P" };
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, JValue.CreateString("xs:notatype"), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
    }
}
