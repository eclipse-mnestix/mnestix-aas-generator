using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder;

namespace Core.Tests.TemplateBuilder;

public class TemplateCreatorTest
{
    [Test]
    public async Task AddNewSubmodelInTemplateAasAsync_WhenSubmodelContainsDateValue_PreservesExactFormat()
    {
        // ARRANGE
        const string templatesAasId = "http://test.template.aas.id";
        const string repoProxyAasPath = "/testpath/for/AasPath";
        const string repoProxySubmodelPath = "/testpath/for/SubmodelPath";
        const string dateValue = "2024-01-01T10:15:30.100Z";

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = repoProxyAasPath,
            SubmodelPath = repoProxySubmodelPath
        };

        var configurationOptions = new ConfigurationOptions
        {
            TemplatesAasId = templatesAasId
        };

        var template = $$"""
            {
              "id": "https://example.com/sm/template-1",
              "idShort": "Nameplate",
              "submodelElements": [
                { "modelType": "Property", "valueType": "xs:dateTime", "idShort": "InstallDate", "value": "{{dateValue}}" }
              ]
            }
            """;

        string? capturedSubmodelJson = null;
        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock
            .Setup(s => s.PostAsync(repoProxySubmodelPath, It.IsAny<string>()))
            .Callback<string, string>((_, json) => capturedSubmodelJson = json)
            .ReturnsAsync((string?)null);
        repoProxyClientMock
            .Setup(s => s.PostAsync(It.Is<string>(path => path != repoProxySubmodelPath), It.IsAny<string>()))
            .ReturnsAsync((string?)null);

        var templateCreator = new TemplateCreator(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(repoProxyOptions),
            new Mock<ILogger<TemplateCreator>>().Object);

        // ACT
        await templateCreator.AddNewSubmodelInTemplateAasAsync(template);

        // ASSERT
        capturedSubmodelJson.Should().NotBeNull();
        capturedSubmodelJson.Should().Contain($"\"{dateValue}\"");
    }
}
