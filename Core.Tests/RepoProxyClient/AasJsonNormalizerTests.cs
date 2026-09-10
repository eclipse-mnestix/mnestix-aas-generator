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
    public void NormalizeJsonForRepository_DataSpecificationArray_IsStripped()
    {
        var input = JObject.Parse("""{"id":"test","dataSpecification":[{}],"hasDataSpecification":[{}]}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("dataSpecification").Should().BeFalse();
        result.ContainsKey("hasDataSpecification").Should().BeFalse();
    }

    [Test]
    public void NormalizeJsonForRepository_DataSpecificationObjectInsideEmbedded_IsKept()
    {
        // In AAS v3, EmbeddedDataSpecification has a required "dataSpecification" (Reference object).
        var input = JObject.Parse("""
        {
            "modelType":"ConceptDescription",
            "id":"test",
            "embeddedDataSpecifications":[{
                "dataSpecification":{"type":"ExternalReference","keys":[{"type":"GlobalReference","value":"urn:example"}]},
                "dataSpecificationContent":{"modelType":"DataSpecificationIec61360","preferredName":[{"language":"en","text":"Test"}]}
            }]
        }
        """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        var embedded = result["embeddedDataSpecifications"]![0] as JObject;
        embedded!.ContainsKey("dataSpecification").Should().BeTrue();
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

    [Test]
    public void NormalizeJsonForRepository_KindWithNoModelType_IsStripped()
    {
        // Objects without modelType (e.g., qualifier-like or legacy template objects)
        // must also have "kind" removed — only explicit Submodel objects may keep it.
        var input = JObject.Parse("""{"type":"SomeType","kind":"Instance"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("kind").Should().BeFalse();
    }

    [Test]
    public void NormalizeJsonForRepository_KindWithV2ObjectModelTypeSubmodel_IsKept()
    {
        // AAS v2 format uses modelType as object: {"name": "Submodel"}
        var input = JObject.Parse("""{"modelType":{"name":"Submodel"},"kind":"Instance","id":"test"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result["kind"]!.Value<string>().Should().Be("Instance");
    }

    [Test]
    public void NormalizeJsonForRepository_KindWithV2ObjectModelTypeNonSubmodel_IsStripped()
    {
        // AAS v2 format modelType object with non-Submodel value must have kind stripped
        var input = JObject.Parse("""{"modelType":{"name":"Property"},"kind":"Instance","value":"test"}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("kind").Should().BeFalse();
    }

    [Test]
    public void NormalizeJsonForRepository_QualifierWithV2ObjectModelType_DoesNotThrow()
    {
        // Qualifiers produced by legacy code may carry v2-style modelType: {"name": "Qualifier"}
        // The normalizer must not crash on this.
        var input = JObject.Parse("""
            {
                "qualifiers": [
                    { "modelType": {"name": "Qualifier"}, "type": "displayName", "valueType": "xs:string", "value": "test" }
                ]
            }
            """);
        var act = () => AasJsonNormalizer.NormalizeJsonForRepository(input);
        act.Should().NotThrow();
        var result = act();
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:string");
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
                    { "modelType": "Qualifier", "type": "MnestixAASGenerator/MappingInfo", "value": "$.name" }
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
                    { "modelType": "Qualifier", "type": "MnestixAASGenerator/MappingInfo", "value": "$.name", "valueType": "" }
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

    // ── Rule 10: empty qualifiers ────────────────────────────────────────────

    [Test]
    public void NormalizeJsonForRepository_EmptyQualifiersArray_IsStripped()
    {
        var input = JObject.Parse("""{"modelType":"Submodel","id":"test","qualifiers":[]}""");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("qualifiers").Should().BeFalse();
    }

    [Test]
    public void NormalizeJsonForRepository_NonEmptyQualifiersArray_IsKept()
    {
        var input = JObject.Parse("""
            {
                "modelType": "Submodel",
                "id": "test",
                "qualifiers": [
                    { "type": "MnestixAASGenerator/MappingInfo", "value": "$.x", "valueType": "xs:string" }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("qualifiers").Should().BeTrue();
        result["qualifiers"]!.Should().HaveCount(1);
    }
    
    [Test]
    public void NormalizeJsonForRepository_NonEmptyQualifiersArray_AllPropertiesPreserved()
    {
        var input = JObject.Parse("""
            {
                "modelType": "Submodel",
                "id": "test",
                "qualifiers": [
                    { "type": "MnestixAASGenerator/MappingInfo", "value": "$.x", "valueType": "xs:string", "kind": "TemplateQualifier" }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("qualifiers").Should().BeTrue();
        result["qualifiers"]!.Should().HaveCount(1);

        var qualifier = (JObject)result["qualifiers"]![0]!;
        qualifier["type"]!.Value<string>().Should().Be("MnestixAASGenerator/MappingInfo");
        qualifier["value"]!.Value<string>().Should().Be("$.x");
        qualifier["valueType"]!.Value<string>().Should().Be("xs:string");
        qualifier["kind"]!.Value<string>().Should().Be("TemplateQualifier");
    }

    [Test]
    public void NormalizeJsonForRepository_NestedEmptyQualifiers_AreStripped()
    {
        var input = JObject.Parse("""
            {
                "modelType": "Submodel",
                "id": "test",
                "qualifiers": [],
                "submodelElements": [
                    {
                        "modelType": "Property",
                        "valueType": "xs:string",
                        "value": "hello",
                        "qualifiers": []
                    }
                ]
            }
            """);
        var result = AasJsonNormalizer.NormalizeJsonForRepository(input);
        result.ContainsKey("qualifiers").Should().BeFalse();
        var element = (JObject)result["submodelElements"]![0]!;
        element.ContainsKey("qualifiers").Should().BeFalse();
    }
}
