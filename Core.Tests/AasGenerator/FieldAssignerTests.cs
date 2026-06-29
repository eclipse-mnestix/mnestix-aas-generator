using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.FieldAssigners;
using MnestixCore.Errors;
using MnestixCore.Shared;
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

    private static JObject MakeElement(string modelType = "Property", string idShort = "P")
    {
        return new JObject { ["modelType"] = modelType, ["idShort"] = idShort };
    }

    private static bool HasWarning(DataMappingContext ctx) =>
        ctx.Logs.Any(l => l.StartsWith("WARNING", StringComparison.Ordinal));

    // ──────────────────────────────────────────────────────────────────────
    // SemanticIdFieldAssigner
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void SemanticId_StringValue_WrappedAsExternalReference()
    {
        var element = MakeElement();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, JValue.CreateString("https://example.com/sid"), "Property", null, MakeContext());

        element["semanticId"]!["type"]!.Value<string>().Should().Be("ExternalReference");
        element["semanticId"]!["keys"]!.Should().HaveCount(1);
        element["semanticId"]!["keys"]![0]!["type"]!.Value<string>().Should().Be("GlobalReference");
        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be("https://example.com/sid");
    }

    [Test]
    public void SemanticId_ObjectValue_Throws()
    {
        var element = MakeElement();
        var reference = new JObject { ["type"] = "ModelReference", ["keys"] = new JArray() };
        var sut = new SemanticIdFieldAssigner();

        var act = () => sut.Assign(element, reference, "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
        element["semanticId"].Should().BeNull();
    }

    [Test]
    public void SemanticId_PopulatedReferenceObject_StillThrows()
    {
        var element = MakeElement();
        var reference = new JObject
        {
            ["type"] = "ModelReference",
            ["keys"] = new JArray
            {
                new JObject { ["type"] = "Submodel", ["value"] = "urn:sm" },
                new JObject { ["type"] = "Property", ["value"] = "P1" }
            }
        };
        var sut = new SemanticIdFieldAssigner();

        var act = () => sut.Assign(element, reference, "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
    }

    [TestCase(42, "42")]
    [TestCase(0, "0")]
    [TestCase(-7, "-7")]
    public void SemanticId_IntegerValue_StringifiedIntoGlobalReference(int input, string expected)
    {
        var element = MakeElement();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, new JValue(input), "Property", null, MakeContext());

        element["semanticId"]!["type"]!.Value<string>().Should().Be("ExternalReference");
        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be(expected);
    }

    [TestCase(true, "True")]
    [TestCase(false, "False")]
    public void SemanticId_BooleanValue_StringifiedIntoGlobalReference(bool input, string expected)
    {
        var element = MakeElement();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, new JValue(input), "Property", null, MakeContext());

        element["semanticId"]!["type"]!.Value<string>().Should().Be("ExternalReference");
        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be(expected);
    }

    [Test]
    public void SemanticId_FloatValue_StringifiedIntoGlobalReference()
    {
        var element = MakeElement();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, new JValue(1.5d), "Property", null, MakeContext());

        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be("1.5");
    }

    [Test]
    public void SemanticId_NullValue_ProducesEmptyGlobalReferenceValue()
    {
        var element = MakeElement();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, JValue.CreateNull(), "Property", null, MakeContext());

        element["semanticId"]!["type"]!.Value<string>().Should().Be("ExternalReference");
        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().BeEmpty();
    }

    [Test]
    public void SemanticId_ArrayValue_Throws()
    {
        var element = MakeElement();
        var array = new JArray { "a", "b" };
        var sut = new SemanticIdFieldAssigner();

        var act = () => sut.Assign(element, array, "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
        element["semanticId"].Should().BeNull();
    }

    [Test]
    public void SemanticId_ObjectOrArray_ExceptionMessageMentionsScalar()
    {
        var element = MakeElement();
        var sut = new SemanticIdFieldAssigner();

        var act = () => sut.Assign(element, new JObject(), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>()
            .Which.Message.Should().Contain("scalar");
    }

    [TestCase("SubmodelElementCollection")]
    [TestCase("SubmodelElementList")]
    [TestCase("Range")]
    [TestCase("File")]
    public void SemanticId_AppliesRegardlessOfModelType(string modelType)
    {
        var element = MakeElement(modelType);
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, JValue.CreateString("urn:sid"), modelType, null, MakeContext());

        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be("urn:sid");
    }

    [Test]
    public void SemanticId_NoExistingValue_DoesNotWarn()
    {
        var element = MakeElement();
        var ctx = MakeContext();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, JValue.CreateString("urn:sid"), "Property", null, ctx);

        HasWarning(ctx).Should().BeFalse();
    }

    [Test]
    public void SemanticId_OverridingExistingValue_LogsWarning()
    {
        var element = MakeElement();
        element["semanticId"] = new JObject
        {
            ["type"] = "ExternalReference",
            ["keys"] = new JArray { new JObject { ["type"] = "GlobalReference", ["value"] = "urn:old" } }
        };
        var ctx = MakeContext();
        var sut = new SemanticIdFieldAssigner();

        sut.Assign(element, JValue.CreateString("urn:new"), "Property", null, ctx);

        HasWarning(ctx).Should().BeTrue();
        element["semanticId"]!["keys"]![0]!["value"]!.Value<string>().Should().Be("urn:new");
    }

    // ──────────────────────────────────────────────────────────────────────
    // ValueTypeFieldAssigner
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("xs:string")]
    [TestCase("xs:integer")]
    [TestCase("xs:int")]
    [TestCase("xs:boolean")]
    [TestCase("xs:double")]
    [TestCase("xs:dateTime")]
    [TestCase("xs:anyURI")]
    public void ValueType_ValidType_Assigned(string valueType)
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        sut.Assign(element, JValue.CreateString(valueType), "Property", null, MakeContext());

        element["valueType"]!.Value<string>().Should().Be(valueType);
    }

    [Test]
    public void ValueType_EveryDataTypeDefXsdValue_IsAccepted()
    {
        var sut = new ValueTypeFieldAssigner();

        foreach (var valueType in DataTypeDefXsd.All)
        {
            var element = MakeElement();

            sut.Assign(element, JValue.CreateString(valueType), "Property", null, MakeContext());

            element["valueType"]!.Value<string>().Should().Be(valueType);
        }
    }

    [TestCase("xs:notatype")]
    [TestCase("string")]
    [TestCase("integer")]
    [TestCase("xs:String")]
    [TestCase("XS:STRING")]
    [TestCase("xs:INTEGER")]
    [TestCase(" xs:string")]
    [TestCase("xs:string ")]
    [TestCase("")]
    public void ValueType_InvalidOrNonCanonicalString_Throws(string valueType)
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, JValue.CreateString(valueType), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
        element["valueType"].Should().BeNull();
    }

    [Test]
    public void ValueType_NullValue_Throws()
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, JValue.CreateNull(), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
    }

    [Test]
    public void ValueType_ObjectValue_Throws()
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();
        var objectValue = new JObject { ["foo"] = "xs:string" };

        var act = () => sut.Assign(element, objectValue, "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
    }

    [Test]
    public void ValueType_ArrayValue_Throws()
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();
        var arrayValue = new JArray { "xs:string" };

        var act = () => sut.Assign(element, arrayValue, "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
    }

    [TestCase(5)]
    [TestCase(true)]
    public void ValueType_NonStringScalar_Throws(object input)
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, new JValue(input), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
    }

    [Test]
    public void ValueType_InvalidType_ExceptionMessageListsAllowedValues()
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, JValue.CreateString("xs:nope"), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>()
            .Which.Message.Should().Contain("xs:nope").And.Contain("xs:string");
    }

    [Test]
    public void ValueType_NonCanonicalCasing_Throws_DivergingFromNormalizer()
    {
        // Intentional, pinned behavior: the assigner requires canonical DataTypeDefXsd casing
        // (DataTypeDefXsd.IsValid uses StringComparer.Ordinal) and rejects 'xs:Integer'.
        // This deliberately differs from AasJsonNormalizer, which repairs valueType casing
        // case-insensitively. Mapped valueType values must already be canonical.
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, JValue.CreateString("xs:Integer"), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
        element["valueType"].Should().BeNull();
    }

    [Test]
    public void ValueType_EmptyString_Throws()
    {
        var element = MakeElement();
        var sut = new ValueTypeFieldAssigner();

        var act = () => sut.Assign(element, JValue.CreateString(string.Empty), "Property", null, MakeContext());

        act.Should().Throw<SubmodelDataToInstanceMapperException>();
        element["valueType"].Should().BeNull();
    }

    [Test]
    public void ValueType_NoExistingValue_DoesNotWarn()
    {
        var element = MakeElement();
        var ctx = MakeContext();
        var sut = new ValueTypeFieldAssigner();

        sut.Assign(element, JValue.CreateString("xs:string"), "Property", null, ctx);

        HasWarning(ctx).Should().BeFalse();
    }

    [Test]
    public void ValueType_OverridingExistingValue_LogsWarning()
    {
        var element = MakeElement();
        element["valueType"] = "xs:string";
        var ctx = MakeContext();
        var sut = new ValueTypeFieldAssigner();

        sut.Assign(element, JValue.CreateString("xs:integer"), "Property", null, ctx);

        HasWarning(ctx).Should().BeTrue();
        element["valueType"]!.Value<string>().Should().Be("xs:integer");
    }
}
