using FluentAssertions;
using Microsoft.Extensions.Logging;
using MnestixCore.Shared;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.Shared;

[TestFixture]
public class SubmodelHandlerTests
{
    private SubmodelHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerMock = new Mock<ILogger<SubmodelHandler>>();
        _handler = new SubmodelHandler(loggerMock.Object);
    }

    [Test]
    public void GetSubmodelsIdsFromSubmodelsRefs_NullResult_ReturnsEmptyList()
    {
        var input = JObject.Parse("""{"paging_metadata":{}}""");

        var result = _handler.GetSubmodelsIdsFromSubmodelsRefs(input);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetSubmodelsIdsFromSubmodelsRefs_EmptyResultArray_ReturnsEmptyList()
    {
        var input = JObject.Parse("""{"result":[]}""");

        var result = _handler.GetSubmodelsIdsFromSubmodelsRefs(input);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetSubmodelsIdsFromSubmodelsRefs_PopulatedResult_ReturnsAllSubmodelIds()
    {
        var input = JObject.Parse("""
            {
                "result": [
                    { "keys": [{ "type": "Submodel", "value": "urn:sm:1" }] },
                    { "keys": [{ "type": "Submodel", "value": "urn:sm:2" }] },
                    { "keys": [{ "type": "Submodel", "value": "urn:sm:3" }] }
                ]
            }
            """);

        var result = _handler.GetSubmodelsIdsFromSubmodelsRefs(input);

        result.Should().Equal("urn:sm:1", "urn:sm:2", "urn:sm:3");
    }

    [Test]
    public void GetSubmodelsIdsFromSubmodelsRefs_MalformedReferenceSkipped_ReturnsOnlyValidIds()
    {
        var input = JObject.Parse("""
            {
                "result": [
                    { "keys": [{ "type": "Submodel", "value": "urn:sm:valid1" }] },
                    { "keys": [] },
                    { "keys": [{ "type": "Submodel", "value": "" }] },
                    { "keys": [{ "type": "Submodel", "value": "urn:sm:valid2" }] }
                ]
            }
            """);

        var result = _handler.GetSubmodelsIdsFromSubmodelsRefs(input);

        result.Should().Equal("urn:sm:valid1", "urn:sm:valid2");
    }
}
