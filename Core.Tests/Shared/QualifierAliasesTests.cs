using FluentAssertions;
using MnestixCore.Shared;

namespace Core.Tests.Shared;

[TestFixture]
public class QualifierAliasesTests
{
    [TestCase("SMT/MappingInfo", "MnestixAASGenerator/MappingInfo")]
    [TestCase("SMT/MappingInfo/value", "MnestixAASGenerator/MappingInfo/value")]
    [TestCase("SMT/MappingInfo/semanticId", "MnestixAASGenerator/MappingInfo/semanticId")]
    [TestCase("SMT/CollectionMappingInfo", "MnestixAASGenerator/CollectionMappingInfo")]
    [TestCase("SMT/FilterMappingInfo", "MnestixAASGenerator/FilterMappingInfo")]
    public void Canonicalize_LegacyType_ReturnsNewPrefix(string input, string expected)
    {
        QualifierAliases.Canonicalize(input).Should().Be(expected);
    }

    [TestCase("MnestixAASGenerator/MappingInfo")]
    [TestCase("MnestixAASGenerator/MappingInfo/value")]
    [TestCase("MnestixAASGenerator/CollectionMappingInfo")]
    [TestCase("MnestixAASGenerator/FilterMappingInfo")]
    public void Canonicalize_AlreadyCanonical_ReturnsUnchanged(string input)
    {
        QualifierAliases.Canonicalize(input).Should().Be(input);
    }

    // SMT/Cardinality is an IDTA standard qualifier and must never be renamed; custom
    // qualifiers must pass through untouched (map-based, not blind prefix stripping).
    [TestCase("SMT/Cardinality")]
    [TestCase("SMT/MappingInfoExtra")] // not a real segment boundary -> untouched
    [TestCase("SomeVendor/CustomQualifier")]
    [TestCase("")]
    public void Canonicalize_NotInMap_ReturnsUnchanged(string input)
    {
        QualifierAliases.Canonicalize(input).Should().Be(input);
    }
}
