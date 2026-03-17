using Core.Tests.TestFiles;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Text;
using Web.Tests.IntegrationTests.Shared;


namespace Web.Tests.IntegrationTests
{
    public class AddDataToAasTests : IntegrationTestsBase
    {
        [Test]
        public async Task AddDataToSubmodel_ShouldCreateNewSubmodelWithDynamicData()
        {
            // ARRANGE
            var blueprintSubmodel = TestFileProvider.GetExampleBlueprintJson();
            var blueprintIdBase64 = "TmFtZXBsYXRlX1RlbXBsYXRlXzViZjBkZjk4LWUxNDMtNDdiMS04ZDNlLTQyMTgwYjQwODg2Yg";
            var aasId = "someRandomAASWhichDoesNotExists";
            var submodels = new List<JObject>();

            var serialNumberTest = "123456789";
            var manufacturerNameTest = "Test Manufacturer";
            var companyTest = "Company GmbH";

            var json = CreateJsonPayload(serialNumberTest, manufacturerNameTest, companyTest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var mockedRestClient = new MockRestClientBuilder(submodels: submodels)
                .WithGetSubmodel(blueprintIdBase64, blueprintSubmodel, HttpStatusCode.OK)
                .WithGetIdSettings()
                .WithPostSubmodelRefs(aasId)
                .WithPostSubmodel()
                .Build();

            Func<IRestClient> restClientFactory = () => mockedRestClient;
            HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);
            

            //ACT
            var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync($"/api/DataIngest/{aasId}", content);

            //ASSERT
            submodels.Should().HaveCount(1);
            var addedSubmodel = submodels[0];
            var elements = addedSubmodel["submodelElements"] as JArray;

            var elementDict = elements?
                .OfType<JObject>()
                .ToDictionary(e => e["idShort"]?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);

            AssertSimpleElement(elementDict, "SerialNumber", serialNumberTest);
            AssertTextValueElement(elementDict, "ManufacturerName", manufacturerNameTest);
            AssertNestedElement(elementDict, "ContactInformation", "Company", companyTest);
        }

        private static string CreateJsonPayload(string serial, string manufacturer, string company)
        {
            return $@"
                {{
                  ""language"": ""de"",
                  ""data"": {{
                    ""SerialNumber"": ""{serial}"",
                    ""ManufacturerName"": ""{manufacturer}"",
                    ""ContactInformation"": {{
                      ""Company"": ""{company}""
                    }}
                  }},
                  ""blueprintsIds"": [
                    ""Nameplate_Template_5bf0df98-e143-47b1-8d3e-42180b40886b""
                  ]
                }}";
        }
        private static void AssertSimpleElement(Dictionary<string, JObject>? elementDict, string key, string expectedValue)
        {
            elementDict.Should().ContainKey(key);
            elementDict![key]["value"]?.ToString().Should().Be(expectedValue);
        }
        private static void AssertTextValueElement(Dictionary<string, JObject>? elementDict, string key, string expectedText)
        {
            elementDict.Should().ContainKey(key);
            var descriptions = elementDict![key]["value"];
            var text = descriptions?[0]?["text"]?.ToString();
            text.Should().Be(expectedText);
        }
        private static void AssertNestedElement(Dictionary<string, JObject>? elementDict, string topLevelIdShort, string lowLevelIdShort, string expectedValue)
        {
            elementDict.Should().ContainKey(topLevelIdShort);
            var nestedElement = elementDict![topLevelIdShort]["value"]?.FirstOrDefault(el => el["idShort"]?.ToString() == lowLevelIdShort);

            nestedElement.Should().NotBeNull();
            nestedElement?["value"]?[0]?["text"]?.ToString().Should().Be(expectedValue);
        }
    }
}
