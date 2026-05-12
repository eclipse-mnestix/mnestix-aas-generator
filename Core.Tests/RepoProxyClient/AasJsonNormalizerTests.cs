using FluentAssertions;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace Core.Tests.RepoProxyClient;

[TestFixture]
public class AasJsonNormalizerTests
{
    // ── Rule 1: null properties ──────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_NullProperties_AreStripped()
    {
        var input = JObject.Parse("""{"id":"test","description":null}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("description").Should().BeFalse();
        result["id"]!.Value<string>().Should().Be("test");
    }

    // ── Rule 2: dataSpecification ────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_DataSpecification_IsStripped()
    {
        var input = JObject.Parse("""{"id":"test","dataSpecification":[{}],"hasDataSpecification":[{}]}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("dataSpecification").Should().BeFalse();
        result.ContainsKey("hasDataSpecification").Should().BeFalse();
    }

    // ── Rule 3: kind ─────────────────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_KindOnSubmodel_IsKept()
    {
        var input = JObject.Parse("""{"modelType":"Submodel","kind":"Instance"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["kind"]!.Value<string>().Should().Be("Instance");
    }

    [Test]
    public void NormalizeJsonForRepository_KindOnNonSubmodel_IsStripped()
    {
        var input = JObject.Parse("""{"modelType":"Property","kind":"Instance","value":"test"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("kind").Should().BeFalse();
    }

    // ── Rule 4: parent ───────────────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_ParentReference_IsStripped()
    {
        var input = JObject.Parse("""{"id":"test","parent":{"keys":[]}}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("parent").Should().BeFalse();
    }

    // ── AAS v2 leftovers ─────────────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_V2KeyFields_AreStripped()
    {
        var input = JObject.Parse("""{"type":"Submodel","value":"id","local":true,"idType":"IRI","index":0}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("local").Should().BeFalse();
        result.ContainsKey("idType").Should().BeFalse();
        result.ContainsKey("index").Should().BeFalse();
    }

    [Test]
    public void NormalizeJsonForRepository_V2CollectionFields_AreStripped()
    {
        var input = JObject.Parse("""{"modelType":"SubmodelElementCollection","ordered":true,"allowDuplicates":false}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("ordered").Should().BeFalse();
        result.ContainsKey("allowDuplicates").Should().BeFalse();
    }

    // ── Rule 5: valueType casing ─────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_ValueType_IsNormalizedToCanonicalCase()
    {
        var input = JObject.Parse("""{"modelType":"Property","valueType":"XS:STRING"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["valueType"]!.Value<string>().Should().Be("xs:string");
    }

    // ── Rule 6: qualifier valueType injection ────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_QualifierMissingValueType_GetsXsString()
    {
        var input = JObject.Parse("""
            {
                "qualifiers": [
                    { "type": "SomeQualifier", "value": "someValue" }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:string");
    }

    [Test]
    public void NormalizeJsonForRepository_QualifierWithModelTypeQualifier_GetsXsString()
    {
        // Real AAS v3 qualifiers carry "modelType": "Qualifier" — Rule 6 must still fire.
        var input = JObject.Parse("""
            {
                "qualifiers": [
                    { "modelType": "Qualifier", "type": "SMT/MappingInfo", "value": "$.name" }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:string");
    }

    [Test]
    public void NormalizeJsonForRepository_QualifierWithEmptyStringValueType_IsReplaced()
    {
        var input = JObject.Parse("""
            {
                "qualifiers": [
                    { "modelType": "Qualifier", "type": "SMT/MappingInfo", "value": "$.name", "valueType": "" }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:string");
    }

    [Test]
    public void NormalizeJsonForRepository_QualifierWithExistingValueType_IsNotOverridden()
    {
        var input = JObject.Parse("""
            {
                "qualifiers": [
                    { "type": "SomeQualifier", "value": "42", "valueType": "xs:int" }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:int");
    }

    // ── Rule 7: Property.value coercion ──────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_PropertyIntegerValue_IsCoercedToString()
    {
        var input = JObject.Parse("""{"modelType":"Property","valueType":"xs:int","value":42}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["value"]!.Type.Should().Be(JTokenType.String);
        result["value"]!.Value<string>().Should().Be("42");
    }

    [Test]
    public void NormalizeJsonForRepository_PropertyFloatValue_IsCoercedToString()
    {
        var input = JObject.Parse("""{"modelType":"Property","valueType":"xs:double","value":3.14}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["value"]!.Type.Should().Be(JTokenType.String);
        result["value"]!.Value<string>().Should().Be("3.14");
    }

    [Test]
    public void NormalizeJsonForRepository_PropertyBooleanTrue_IsCoercedToLowercaseString()
    {
        var input = JObject.Parse("""{"modelType":"Property","valueType":"xs:boolean","value":true}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["value"]!.Type.Should().Be(JTokenType.String);
        result["value"]!.Value<string>().Should().Be("true");
    }

    [Test]
    public void NormalizeJsonForRepository_PropertyBooleanFalse_IsCoercedToLowercaseString()
    {
        var input = JObject.Parse("""{"modelType":"Property","valueType":"xs:boolean","value":false}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["value"]!.Type.Should().Be(JTokenType.String);
        result["value"]!.Value<string>().Should().Be("false");
    }

    [Test]
    public void NormalizeJsonForRepository_PropertyStringValue_IsNotChanged()
    {
        var input = JObject.Parse("""{"modelType":"Property","valueType":"xs:string","value":"hello"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["value"]!.Type.Should().Be(JTokenType.String);
        result["value"]!.Value<string>().Should().Be("hello");
    }

    // ── Recursion ────────────────────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_NestedElements_AreAlsoNormalized()
    {
        var input = JObject.Parse("""
            {
                "modelType": "Submodel",
                "submodelElements": [
                    {
                        "modelType": "Property",
                        "valueType": "xs:boolean",
                        "value": true,
                        "parent": { "keys": [] }
                    }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        var element = (JObject)result["submodelElements"]![0]!;
        element["value"]!.Value<string>().Should().Be("true");
        element.ContainsKey("parent").Should().BeFalse();
    }
}
