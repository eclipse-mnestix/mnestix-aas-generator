using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MnestixCore.Shared;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Text;
using Web.Tests.IntegrationTests.Shared;

namespace Web.Tests.IntegrationTests;

public class AasCreatorEndpointTests : IntegrationTestsBase
{

    [Test]
    public async Task CreateAas_WithoutRequestBody_ShouldReturnCreated()
    {
        // ARRANGE
        var mockedRestClient = new MockRestClientBuilder()
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFz", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func <IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAas", null, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixcreateAas\"");
        responseContent.Should().Contain("\"submodelResults\":[]");
    }

    [Test]
    public async Task CreateAas_WithSubmodels_ShouldReturnCreatedWithSubmodelResultsAndShellRefs()
    {
        // ARRANGE
        var blueprintSubmodel = TestFileProvider.GetExampleBlueprintJson();
        var blueprintIdBase64 = "TmFtZXBsYXRlX1RlbXBsYXRlXzViZjBkZjk4LWUxNDMtNDdiMS04ZDNlLTQyMTgwYjQwODg2Yg";
        var submodels = new List<JObject>();
        var aasList = new List<JObject>();

        var serialNumberTest = "123456789";
        var manufacturerNameTest = "Test Manufacturer";

        var json = $@"
            {{
              ""language"": ""de"",
              ""data"": {{
                ""SerialNumber"": ""{serialNumberTest}"",
                ""ManufacturerName"": ""{manufacturerNameTest}""
              }},
              ""blueprintsIds"": [
                ""Nameplate_Template_5bf0df98-e143-47b1-8d3e-42180b40886b""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList, submodels: submodels)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFzV2l0aFN1Ym1vZGVscw", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .WithGetSubmodel(blueprintIdBase64, blueprintSubmodel, HttpStatusCode.OK)
            .WithPostSubmodel()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAasWithSubmodels", content, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixcreateAasWithSubmodels\"");
        responseContent.Should().Contain("\"submodelResults\":");
        aasList.Should().HaveCount(1);
        submodels.Should().HaveCount(1);

        // submodel must be persisted before the shell, and the shell must carry the ref
        var persistedSubmodelId = submodels[0]["id"]?.ToString();
        persistedSubmodelId.Should().NotBeNullOrEmpty();
        var shellRefs = aasList[0]["submodels"] as JArray;
        shellRefs.Should().NotBeNull();
        shellRefs!.ToString().Should().Contain(persistedSubmodelId!);

        var addedSubmodel = submodels[0];
        var elements = addedSubmodel["submodelElements"] as JArray;
        var elementDict = elements?
            .OfType<JObject>()
            .ToDictionary(e => e["idShort"]?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);

        elementDict.Should().ContainKey("SerialNumber");
        elementDict!["SerialNumber"]["value"]?.ToString().Should().Be(serialNumberTest);
    }

    [Test]
    public async Task CreateAas_OverwriteTrue_ExistingShell_ShouldReturnOkWithPreviousAas()
    {
        // ARRANGE — POST shell returns 409, then GET old + PUT new
        var aasBase64 = "aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvb3ZlcndyaXRlTWU";
        var oldShell = "{\"id\":\"old-shell-id\",\"idShort\":\"old\"}";
        var aasList = new List<JObject>();

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAasConflict()
            .WithGetAas(aasBase64, oldShell, HttpStatusCode.OK)
            .WithPutAas(aasBase64)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/overwriteMe?overwrite=true", null, StatusCodes.Status200OK);

        // ASSERT
        responseContent.Should().Contain("\"previousAas\":");
        responseContent.Should().Contain("old-shell-id");
        aasList.Should().HaveCount(1); // the PUT body
    }

    [Test]
    public async Task CreateAas_OverwriteFalse_ExistingShell_ShouldReturnConflict()
    {
        // ARRANGE — POST shell returns 409 and overwrite=false
        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .WithPostAasConflict()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/conflictMe", null, StatusCodes.Status409Conflict);

        // ASSERT
        var conflict = JObject.Parse(responseContent);
        conflict.Should().ContainKey("error");
        conflict["error"]!.ToString().Should().NotBeNullOrEmpty();
        conflict.Should().ContainKey("orphanedSubmodelIds");
    }

    [Test]
    public async Task CreateAas_WithInvalidBlueprint_ShouldReturnBadRequest_AndNotCreateShell()
    {
        // ARRANGE
        var blueprintIdBase64 = "aW52YWxpZEJsdWVwcmludElk"; // invalidBlueprintId
        var aasList = new List<JObject>();

        var json = $@"
            {{
              ""language"": ""de"",
              ""data"": {{}},
              ""blueprintsIds"": [
                ""invalidBlueprintId""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAas()
            .WithGetSubmodel(blueprintIdBase64, "", HttpStatusCode.NotFound, false)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT & ASSERT — build fails in memory, so the shell is never POSTed
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAasFail", content, StatusCodes.Status400BadRequest);

        aasList.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAas_WithInvalidAssetKind_ShouldReturnBadRequest()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""assetKind"": ""InvalidValue""
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT & ASSERT — ASP.NET Core enum validation rejects invalid AssetKind values
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/testAsset", content, StatusCodes.Status400BadRequest);

        // Shell should never be created due to validation failure
        aasList.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAas_WithValidTypeAssetKind_ShouldReturnCreated()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""assetKind"": ""Type""
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvdHlwZUFzc2V0", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/typeAsset", content, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixtypeAsset\"");
        aasList.Should().HaveCount(1);

        // Verify the created AAS has assetKind: Type
        var createdAas = aasList[0];
        createdAas["assetInformation"]?["assetKind"]?.ToString().Should().Be("Type");
    }

    [Test]
    public async Task CreateAas_WithExtensions_ShouldAddExtensionsToAas()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""extensions"": {
                ""manufacturer"": ""ACME Corp"",
                ""location"": ""Building A""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvdGVzdEV4dGVuc2lvbnM", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/testExtensions", content, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixtestExtensions\"");
        aasList.Should().HaveCount(1);

        var createdAas = aasList[0];
        var extensions = createdAas["extensions"] as JArray;
        extensions.Should().NotBeNull();
        extensions!.Should().HaveCount(2);

        var manufacturerExt = extensions.FirstOrDefault(e => e["name"]?.ToString() == "manufacturer");
        manufacturerExt.Should().NotBeNull();
        manufacturerExt!["value"]?.ToString().Should().Be("ACME Corp");

        var locationExt = extensions.FirstOrDefault(e => e["name"]?.ToString() == "location");
        locationExt.Should().NotBeNull();
        locationExt!["value"]?.ToString().Should().Be("Building A");
    }

    [Test]
    public async Task CreateAas_WithEmptyExtensions_ShouldNotAddExtensionsField()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""extensions"": {}
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvZW1wdHlFeHQ", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/emptyExt", content, StatusCodes.Status201Created);

        // ASSERT
        aasList.Should().HaveCount(1);
        var createdAas = aasList[0];
        createdAas["extensions"].Should().BeNull();
    }

    [Test]
    public async Task CreateAas_WithSpecificAssetIds_ShouldAddToAssetInformation()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""specificAssetIds"": [
                { ""name"": ""SerialNumber"", ""value"": ""SN-12345"" },
                { ""name"": ""PartNumber"", ""value"": ""PN-ABC-001"" }
              ]
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvdGVzdFNwZWNpZmljSWRz", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/testSpecificIds", content, StatusCodes.Status201Created);

        // ASSERT
        aasList.Should().HaveCount(1);
        var createdAas = aasList[0];
        var assetInfo = createdAas["assetInformation"] as JObject;
        assetInfo.Should().NotBeNull();

        var specificIds = assetInfo!["specificAssetIds"] as JArray;
        specificIds.Should().NotBeNull();
        specificIds!.Should().HaveCount(3); // default assetIdShort + 2 custom ones

        var serialNumber = specificIds.FirstOrDefault(id => id["name"]?.ToString() == "SerialNumber");
        serialNumber.Should().NotBeNull();
        serialNumber!["value"]?.ToString().Should().Be("SN-12345");

        var partNumber = specificIds.FirstOrDefault(id => id["name"]?.ToString() == "PartNumber");
        partNumber.Should().NotBeNull();
        partNumber!["value"]?.ToString().Should().Be("PN-ABC-001");
    }

    [Test]
    public async Task CreateAas_WithInvalidSpecificAssetIds_MissingName_ShouldReturnBadRequest()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""specificAssetIds"": [
                { ""value"": ""SN-12345"" }
              ]
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT & ASSERT
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/invalidSpecificIds", content, StatusCodes.Status400BadRequest);
        aasList.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAas_WithAdministration_ShouldAddVersionAndRevision()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""administration"": {
                ""version"": ""1.0"",
                ""revision"": ""2""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvdGVzdEFkbWlu", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/testAdmin", content, StatusCodes.Status201Created);

        // ASSERT
        aasList.Should().HaveCount(1);
        var createdAas = aasList[0];
        var administration = createdAas["administration"] as JObject;
        administration.Should().NotBeNull();
        administration!["version"]?.ToString().Should().Be("1.0");
        administration["revision"]?.ToString().Should().Be("2");
    }

    [Test]
    public async Task CreateAas_WithAdministrationVersionOnly_ShouldNotIncludeRevision()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""administration"": {
                ""version"": ""2.0""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvdGVzdEFkbWluVmVy", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/testAdminVer", content, StatusCodes.Status201Created);

        // ASSERT
        aasList.Should().HaveCount(1);
        var createdAas = aasList[0];
        var administration = createdAas["administration"] as JObject;
        administration.Should().NotBeNull();
        administration!["version"]?.ToString().Should().Be("2.0");
        administration["revision"].Should().BeNull();
    }

    [Test]
    public async Task CreateAas_WithInvalidAdministration_MissingVersion_ShouldReturnBadRequest()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""administration"": {
                ""revision"": ""1""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT & ASSERT
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/invalidAdmin", content, StatusCodes.Status400BadRequest);
        aasList.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAas_WithAllOptionalFields_ShouldCreateCompleteAas()
    {
        // ARRANGE
        var aasList = new List<JObject>();

        var json = @"
            {
              ""assetKind"": ""Type"",
              ""extensions"": {
                ""manufacturer"": ""ACME Corp"",
                ""category"": ""Industrial""
              },
              ""specificAssetIds"": [
                { ""name"": ""SerialNumber"", ""value"": ""SN-99999"" }
              ],
              ""administration"": {
                ""version"": ""3.0"",
                ""revision"": ""5""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY29tcGxldGVBYXM", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/completeAas", content, StatusCodes.Status201Created);

        // ASSERT
        aasList.Should().HaveCount(1);
        var createdAas = aasList[0];

        // Verify assetKind
        createdAas["assetInformation"]?["assetKind"]?.ToString().Should().Be("Type");

        // Verify extensions
        var extensions = createdAas["extensions"] as JArray;
        extensions.Should().NotBeNull().And.HaveCount(2);

        // Verify specificAssetIds
        var specificIds = (createdAas["assetInformation"] as JObject)?["specificAssetIds"] as JArray;
        specificIds.Should().NotBeNull().And.HaveCountGreaterOrEqualTo(2); // default + custom

        // Verify administration
        var administration = createdAas["administration"] as JObject;
        administration.Should().NotBeNull();
        administration!["version"]?.ToString().Should().Be("3.0");
        administration["revision"]?.ToString().Should().Be("5");
    }

    [Test]
    public async Task CreateAas_WithSubmodelIds_ShouldLinkExistingSubmodels()
    {
        // ARRANGE
        var existingSubmodelId1 = "https://example.com/submodels/existing-sm-1";
        var existingSubmodelId2 = "https://example.com/submodels/existing-sm-2";
        var existingSubmodelId1Base64 = Base64StringDeAndEncoder.EncodeTo64(existingSubmodelId1);
        var existingSubmodelId2Base64 = Base64StringDeAndEncoder.EncodeTo64(existingSubmodelId2);

        var aasList = new List<JObject>();

        var json = $@"
            {{
              ""submodelIds"": [
                ""{existingSubmodelId1}"",
                ""{existingSubmodelId2}""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvd2l0aFN1Ym1vZGVsSWRz", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithGetSubmodel(existingSubmodelId1Base64, "{\"id\":\"" + existingSubmodelId1 + "\"}", HttpStatusCode.OK)
            .WithGetSubmodel(existingSubmodelId2Base64, "{\"id\":\"" + existingSubmodelId2 + "\"}", HttpStatusCode.OK)
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/withSubmodelIds", content, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixwithSubmodelIds\"");
        aasList.Should().HaveCount(1);

        // Verify shell has references to both provided submodel IDs
        var shellRefs = aasList[0]["submodels"] as JArray;
        shellRefs.Should().NotBeNull().And.HaveCount(2);
        shellRefs!.ToString().Should().Contain(existingSubmodelId1);
        shellRefs.ToString().Should().Contain(existingSubmodelId2);
    }

    [Test]
    public async Task CreateAas_WithBlueprintsAndSubmodelIds_ShouldCreateAndLinkSubmodels()
    {
        // ARRANGE
        var blueprintSubmodel = TestFileProvider.GetExampleBlueprintJson();
        var blueprintIdBase64 = "TmFtZXBsYXRlX1RlbXBsYXRlXzViZjBkZjk4LWUxNDMtNDdiMS04ZDNlLTQyMTgwYjQwODg2Yg";
        var existingSubmodelId = "https://example.com/submodels/existing-sm";
        var existingSubmodelIdBase64 = Base64StringDeAndEncoder.EncodeTo64(existingSubmodelId);

        var submodels = new List<JObject>();
        var aasList = new List<JObject>();

        var json = $@"
            {{
              ""language"": ""de"",
              ""data"": {{
                ""SerialNumber"": ""12345"",
                ""ManufacturerName"": ""Test""
              }},
              ""blueprintsIds"": [
                ""Nameplate_Template_5bf0df98-e143-47b1-8d3e-42180b40886b""
              ],
              ""submodelIds"": [
                ""{existingSubmodelId}""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList, submodels: submodels)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvbWl4ZWRTdWJtb2RlbHM", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithGetSubmodel(existingSubmodelIdBase64, "{\"id\":\"" + existingSubmodelId + "\"}", HttpStatusCode.OK)
            .WithGetSubmodel(blueprintIdBase64, blueprintSubmodel, HttpStatusCode.OK)
            .WithPostSubmodel()
            .WithPostAas()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/mixedSubmodels", content, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixmixedSubmodels\"");
        aasList.Should().HaveCount(1);
        submodels.Should().HaveCount(1); // only the generated one

        // Verify shell has references to both generated and provided submodels (generated first)
        var shellRefs = aasList[0]["submodels"] as JArray;
        shellRefs.Should().NotBeNull().And.HaveCount(2);

        var generatedSubmodelId = submodels[0]["id"]?.ToString();
        generatedSubmodelId.Should().NotBeNullOrEmpty();

        // Check ordering: generated first, then provided
        shellRefs![0]["keys"]?[0]?["value"]?.ToString().Should().Be(generatedSubmodelId);
        shellRefs[1]["keys"]?[0]?["value"]?.ToString().Should().Be(existingSubmodelId);
    }

    [Test]
    public async Task CreateAas_WithInvalidSubmodelId_ShouldReturnBadRequest()
    {
        // ARRANGE
        var invalidSubmodelId = "https://example.com/submodels/non-existent";
        var invalidSubmodelIdBase64 = Base64StringDeAndEncoder.EncodeTo64(invalidSubmodelId);

        var json = $@"
            {{
              ""submodelIds"": [
                ""{invalidSubmodelId}""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .WithGetSubmodel(invalidSubmodelIdBase64, "", HttpStatusCode.NotFound, false)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/invalidSubmodel", content, StatusCodes.Status400BadRequest);

        // ASSERT
        responseContent.Should().Contain(invalidSubmodelId);
        responseContent.Should().Contain("do not exist");
    }

    [Test]
    public async Task CreateAas_WithAdministrationMissingVersion_ShouldReturnBadRequest()
    {
        // ARRANGE
        var json = @"
            {
              ""administration"": {
                ""revision"": ""2""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/missingVersion", content, StatusCodes.Status400BadRequest);

        // ASSERT
        responseContent.ToLower().Should().Match(s => s.Contains("version") || s.Contains("required"));
    }

    [Test]
    public async Task CreateAas_WithSpecificAssetIdMissingName_ShouldReturnBadRequest()
    {
        // ARRANGE
        var json = @"
            {
              ""specificAssetIds"": [
                {
                  ""value"": ""12345""
                }
              ]
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/missingName", content, StatusCodes.Status400BadRequest);

        // ASSERT
        responseContent.ToLower().Should().Match(s => s.Contains("name") || s.Contains("required"));
    }

    [Test]
    public async Task CreateAas_WithSpecificAssetIdMissingValue_ShouldReturnBadRequest()
    {
        // ARRANGE
        var json = @"
            {
              ""specificAssetIds"": [
                {
                  ""name"": ""SerialNumber""
                }
              ]
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/missingValue", content, StatusCodes.Status400BadRequest);

        // ASSERT
        responseContent.ToLower().Should().Match(s => s.Contains("value") || s.Contains("required"));
    }

    [Test]
    public async Task CreateAas_WithExtensionNameTooLong_ShouldReturnBadRequest()
    {
        // ARRANGE
        var longName = new string('a', 129);
        var json = $@"
            {{
              ""extensions"": {{
                ""{longName}"": ""value1""
              }}
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/longExtName", content, StatusCodes.Status400BadRequest);

        // ASSERT
        responseContent.ToLower().Should().Match(s => s.Contains("extension") && (s.Contains("128") || s.Contains("character")));
    }

    [Test]
    public async Task CreateAas_WithExtensionNameEmpty_ShouldReturnBadRequest()
    {
        // ARRANGE
        var json = @"
            {
              ""extensions"": {
                """": ""value1""
              }
            }";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/emptyExtName", content, StatusCodes.Status400BadRequest);

        // ASSERT
        responseContent.ToLower().Should().Match(s => s.Contains("extension") && (s.Contains("1") || s.Contains("character")));
    }
}
