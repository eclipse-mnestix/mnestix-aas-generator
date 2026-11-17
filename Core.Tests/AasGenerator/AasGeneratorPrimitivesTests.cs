using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder;
using MnestixCore.TemplateBuilder.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

public class AasGeneratorPrimitivesTests
{
    private MnestixCore.AasGenerator.AasGenerator _aasGenerator = null!;
    private IDataMapper _dataToInstanceMapper = null!;
    private Mock<IRepoProxyClient> _repoProxyClientMock = null!;
    private Mock<IBlueprintProvider> _blueprintProviderMock = null!;
    private Mock<IAasIdGeneratorService> _idGeneratorMock = null!;
    private Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>> _loggerMock = null!;
    private const string NewSubmodelId = "TheNewSubmodelId";
    private const string TestSubmodelPath = "/submodels";
    private const string TestAasPath = "/aas";

    [SetUp]
    public void SetUp()
    {
        _dataToInstanceMapper = new DataMapper(new BlueprintValidator(), TimeProvider.System, Options.Create(new AasGeneratorOptions()));
        _repoProxyClientMock = new Mock<IRepoProxyClient>();
        _blueprintProviderMock = new Mock<IBlueprintProvider>();
        _idGeneratorMock = new Mock<IAasIdGeneratorService>();
        _loggerMock = new Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>>();

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = TestAasPath,
            SubmodelPath = TestSubmodelPath,
        };

        _aasGenerator = new MnestixCore.AasGenerator.AasGenerator(
            _dataToInstanceMapper,
            _repoProxyClientMock.Object,
            _blueprintProviderMock.Object,
            _idGeneratorMock.Object,
            Options.Create(repoProxyOptions),
            _loggerMock.Object);

        _idGeneratorMock.Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { NewSubmodelId });
    }

    [Test]
    public async Task BuildSubmodelAsync_ValidBlueprint_ReturnsInstanceAndDoesNotWriteToRepo()
    {
        // ARRANGE
        var blueprint = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var data = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        _blueprintProviderMock.Setup(x => x.GetBlueprintAsync(It.IsAny<string>())).ReturnsAsync(blueprint);

        // ACT
        var built = await _aasGenerator.BuildSubmodelAsync("urn:smtemplate:DemoTemplate", data, "en");

        // ASSERT
        built.Result.Success.Should().BeTrue();
        built.Instance.Should().NotBeNull();
        built.Result.GeneratedSubmodelId.Should().Be(NewSubmodelId);
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task BuildSubmodelAsync_InvalidBlueprint_ReturnsFailureAndDoesNotWriteToRepo()
    {
        // ARRANGE — InputOnlyOptionalField fails mandatory mapping
        var blueprint = DataIngestTestFileProvider.GetTemplateSubmodel("InputOnlyOptionalField");
        var data = DataIngestTestFileProvider.GetData("InputOnlyOptionalField");
        _blueprintProviderMock.Setup(x => x.GetBlueprintAsync(It.IsAny<string>())).ReturnsAsync(blueprint);

        // ACT
        var built = await _aasGenerator.BuildSubmodelAsync("urn:smtemplate:DemoTemplate", data, "en");

        // ASSERT
        built.Result.Success.Should().BeFalse();
        built.Instance.Should().BeNull();
        built.Result.Error!.Message.Should().NotBeNullOrEmpty();
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PostSubmodelAsync_PostsBodyToSubmodelPath_ReturnsId()
    {
        // ARRANGE
        var instance = new JObject { ["id"] = "submodel-xyz", ["modelType"] = "Submodel" };
        string? capturedPath = null;
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, _) => capturedPath = path)
            .ReturnsAsync("created");

        // ACT
        var id = await _aasGenerator.PostSubmodelAsync(instance);

        // ASSERT
        id.Should().Be("submodel-xyz");
        capturedPath.Should().Be(TestSubmodelPath);
        _repoProxyClientMock.Verify(x => x.PostAsync(TestSubmodelPath, It.IsAny<string>()), Times.Once);
    }

    [TestCase("MnestixAASGenerator")]
    [TestCase("SMT")]
    public async Task BuildSubmodelAsync_ValueIdsInAList_PreserveConceptAndDoNotWrite(string prefix)
    {
        var blueprint = JObject.Parse("""
            {
              "id":"urn:example:blueprint", "idShort":"CodedValues", "modelType":"Submodel",
              "submodelElements":[{
                "idShort":"Methods", "modelType":"SubmodelElementList",
                "typeValueListElement":"Property", "valueTypeListElement":"xs:string",
                "value":[{
                  "modelType":"Property", "valueType":"xs:string",
                  "semanticId":{"type":"ExternalReference","keys":[{"type":"GlobalReference","value":"urn:example:calculation-method"}]},
                  "qualifiers":[
                    {"type":"SMT/Cardinality","value":"One"},
                    {"type":"PREFIX/CollectionMappingInfo","value":"methods[*]"},
                    {"type":"PREFIX/MappingInfo/value","value":"methods[*].value"},
                    {"type":"PREFIX/MappingInfo/valueId","value":"methods[*].valueId"}
                  ]
                }]
              }]
            }
            """.Replace("PREFIX", prefix));
        var data = JObject.Parse("""
            {"methods":[
              {"value":"First method","valueId":"urn:example:method:first"},
              {"value":"Second method","valueId":"urn:example:method:second"}
            ]}
            """);
        var original = blueprint.DeepClone();
        _blueprintProviderMock.Setup(x => x.GetBlueprintAsync("dXJuOmV4YW1wbGU6Ymx1ZXByaW50")).ReturnsAsync(blueprint);

        var built = await _aasGenerator.BuildSubmodelAsync("urn:example:blueprint", data, language: null);

        built.Result.Success.Should().BeTrue("{0}", built.Result.Error);
        var entries = (JArray)built.Instance!["submodelElements"]![0]!["value"]!;
        entries.Should().HaveCount(2);
        for (var i = 0; i < entries.Count; i++)
        {
            JToken.DeepEquals(entries[i]["semanticId"], original["submodelElements"]![0]!["value"]![0]!["semanticId"])
                .Should().BeTrue();
            entries[i]["value"]!.Value<string>().Should().Be(data["methods"]![i]!["value"]!.Value<string>());
            JToken.DeepEquals(entries[i]["valueId"], new JObject
            {
                ["type"] = "ExternalReference",
                ["keys"] = new JArray(new JObject
                {
                    ["type"] = "GlobalReference", ["value"] = data["methods"]![i]!["valueId"]!.Value<string>()
                })
            }).Should().BeTrue();
        }
        JToken.DeepEquals(blueprint, original).Should().BeTrue();
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestCase("Property", "missing")]
    [TestCase("Property", "null")]
    [TestCase("Property", "\"\"")]
    [TestCase("MultiLanguageProperty", "missing")]
    [TestCase("MultiLanguageProperty", "null")]
    [TestCase("MultiLanguageProperty", "\"\"")]
    public async Task BuildSubmodelAsync_MissingValueId_DoesNotOmitMandatoryValue(string modelType, string identifier)
    {
        var valueMapping = modelType == "Property" ? "value" : "multiLanguage";
        var blueprint = JObject.Parse("""
            {
              "id":"urn:example:blueprint", "idShort":"CodedValue", "modelType":"Submodel",
              "submodelElements":[{
                "idShort":"Method", "modelType":"MODEL", "valueType":"xs:string",
                "qualifiers":[
                  {"type":"SMT/Cardinality","value":"One"},
                  {"type":"MnestixAASGenerator/MappingInfo/FIELD","value":"value"},
                  {"type":"MnestixAASGenerator/MappingInfo/valueId","value":"valueId"}
                ]
              }]
            }
            """.Replace("MODEL", modelType).Replace("FIELD", valueMapping));
        if (modelType == "MultiLanguageProperty")
            ((JObject)blueprint["submodelElements"]![0]!).Remove("valueType");
        var data = new JObject
        {
            ["value"] = modelType == "Property" ? JValue.CreateString("First method") : new JObject { ["en"] = "First method" }
        };
        if (identifier != "missing") data["valueId"] = JToken.Parse(identifier);
        _blueprintProviderMock.Setup(x => x.GetBlueprintAsync("dXJuOmV4YW1wbGU6Ymx1ZXByaW50")).ReturnsAsync(blueprint);

        var built = await _aasGenerator.BuildSubmodelAsync("urn:example:blueprint", data, language: null);

        built.Result.Success.Should().BeTrue("{0}", built.Result.Error);
        var element = built.Instance!["submodelElements"]![0]!;
        element["valueId"].Should().BeNull();
        var text = modelType == "Property" ? element["value"]!.Value<string>() : element["value"]![0]!["text"]!.Value<string>();
        text.Should().Be("First method");
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PostSubmodelAsync_MissingId_ThrowsAndDoesNotWriteToRepo()
    {
        // ARRANGE
        var instance = new JObject { ["modelType"] = "Submodel" };

        // ACT
        var act = async () => await _aasGenerator.PostSubmodelAsync(instance);

        // ASSERT
        await act.Should().ThrowAsync<ArgumentException>();
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
