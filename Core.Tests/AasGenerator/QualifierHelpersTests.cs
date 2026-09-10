using FluentAssertions;
using MnestixCore.AasGenerator.Pipelines.Shared;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

[TestFixture]
public class QualifierHelpersTests
{
    // Every qualifier type the pipeline builds a recursive path for, plus the internal
    // temp marker used during collection duplication.
    private const string ProcessedCollectionMarker = "_" + QualifierAliases.CollectionMappingInfoType;

    [TestCase(QualifierAliases.CollectionMappingInfoType, "$..qualifiers[?(@.type=='MnestixAASGenerator/CollectionMappingInfo')]")]
    [TestCase(QualifierAliases.FilterMappingInfoType, "$..qualifiers[?(@.type=='MnestixAASGenerator/FilterMappingInfo')]")]
    [TestCase(QualifierAliases.MappingInfoPrefix, "$..qualifiers[?(@.type=='MnestixAASGenerator/MappingInfo')]")]
    [TestCase(ProcessedCollectionMarker, "$..qualifiers[?(@.type=='_MnestixAASGenerator/CollectionMappingInfo')]")]
    [TestCase("SMT/Cardinality", "$..qualifiers[?(@.type=='SMT/Cardinality')]")]
    public void RecursiveQualifierPath_BuildsExpectedPath(string qualifierType, string expectedPath)
    {
        QualifierHelpers.RecursiveQualifierPath(qualifierType).Should().Be(expectedPath);
    }

    [TestCase(QualifierAliases.CollectionMappingInfoType)]
    [TestCase(QualifierAliases.FilterMappingInfoType)]
    [TestCase(QualifierAliases.MappingInfoPrefix)]
    [TestCase(ProcessedCollectionMarker)]
    [TestCase("SMT/Cardinality")]
    public void RecursiveQualifierPath_SelectsOnlyMatchingType_AtAnyDepth(string qualifierType)
    {
        // Two qualifiers of the target type (top-level + nested) and one decoy of a different type.
        var decoyType = qualifierType == "SMT/Cardinality" ? QualifierAliases.MappingInfoPrefix : "SMT/Cardinality";
        var instance = JObject.Parse($$"""
            {
              "qualifiers": [ { "type": "{{qualifierType}}", "value": "top" } ],
              "submodelElements": [
                { "qualifiers": [
                    { "type": "{{qualifierType}}", "value": "nested" },
                    { "type": "{{decoyType}}", "value": "decoy" }
                ] }
              ]
            }
            """);

        var matched = instance.SelectTokens(QualifierHelpers.RecursiveQualifierPath(qualifierType)).ToList();

        matched.Should().HaveCount(2);
        matched.Select(q => q["value"]!.Value<string>()).Should().BeEquivalentTo("top", "nested");
    }

    [Test]
    public void RecursiveQualifierPath_NoMatchingQualifiers_ReturnsEmpty()
    {
        var instance = JObject.Parse("""
            { "qualifiers": [ { "type": "SMT/Cardinality", "value": "One" } ] }
            """);

        var matched = instance.SelectTokens(QualifierHelpers.RecursiveQualifierPath(QualifierAliases.FilterMappingInfoType));

        matched.Should().BeEmpty();
    }

    [Test]
    public void RecursiveQualifierPath_DoesNotMatchPrefixCollisions()
    {
        // "_MnestixAASGenerator/CollectionMappingInfo" (temp marker) and the real type must not
        // be confused with each other: JSONPath equality is exact, not prefix-based.
        var instance = JObject.Parse("""
            {
              "qualifiers": [
                { "type": "MnestixAASGenerator/CollectionMappingInfo", "value": "real" },
                { "type": "_MnestixAASGenerator/CollectionMappingInfo", "value": "marker" }
              ]
            }
            """);

        var real = instance.SelectTokens(QualifierHelpers.RecursiveQualifierPath(QualifierAliases.CollectionMappingInfoType)).ToList();
        var marker = instance.SelectTokens(QualifierHelpers.RecursiveQualifierPath(ProcessedCollectionMarker)).ToList();

        real.Should().ContainSingle().Which["value"]!.Value<string>().Should().Be("real");
        marker.Should().ContainSingle().Which["value"]!.Value<string>().Should().Be("marker");
    }
}
