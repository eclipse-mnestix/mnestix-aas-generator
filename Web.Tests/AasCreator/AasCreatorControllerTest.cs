using MnestixCore.AasCreator;
using MnestixCore.AasCreator.Interfaces;
using MnestixCore.AasGenerator;
using MnestixCore.Dtos;
using MnestixCore.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MnestixApi.Controllers;
using Newtonsoft.Json.Linq;

namespace Web.Tests.AasCreator;

public class AasCreatorControllerTest
{
    [Test]
    public async Task CreateAas_WithoutRequestBody_ReturnStatus200()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var assetId = "https://www.example.com/" + assetIdShort;
        var aasIdShort = "aas_" + assetIdShort;
        var aasId = "https://www.example.com/aas/" + assetIdShort;
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasId);
        var base64EncodedAssetId = Base64StringDeAndEncoder.EncodeTo64(assetId);

        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), null, null, null, It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(
                new AasIds(assetId, assetIdShort, aasId, aasIdShort), 
                AasCreationStatus.Created,
                Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT 
        var result = await controller.CreateAas(assetIdShort, null);

        // ASSERT
        Assert.IsInstanceOf<ActionResult<CreateAasResponse>>(result);
        Assert.IsInstanceOf<OkObjectResult>(result.Result);
        var actionResult = result.Result as OkObjectResult;
        actionResult.Should().NotBeNull();
        actionResult?.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = actionResult?.Value as CreateAasResponse;
        response.Should().NotBeNull();
        response?.AssetId.Should().Be(assetId);
        response?.AasId.Should().Be(aasId);
        response?.Base64EncodedAssetId.Should().Be(base64EncodedAssetId);
        response?.Base64EncodedAasId.Should().Be(base64EncodedAasId);
        response?.SubmodelResults.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAas_AasIdAlreadyExists_ReturnStatus400()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var aasId = "https://www.example.com/aas/" + assetIdShort;
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), null, null, null, It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(
                new AasIds("", "", aasId, ""), 
                AasCreationStatus.AlreadyExists,
                Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT 
        var result = await controller.CreateAas(assetIdShort, null);

        // ASSERT
        Assert.IsInstanceOf<ActionResult<CreateAasResponse>>(result);
        Assert.IsInstanceOf<BadRequestObjectResult>(result.Result);
        var actionResult = result.Result as BadRequestObjectResult;
        actionResult.Should().NotBeNull();
        actionResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task CreateAas_WithSubmodels_ReturnStatus200WithSubmodelResults()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var assetId = "https://www.example.com/" + assetIdShort;
        var aasIdShort = "aas_" + assetIdShort;
        var aasId = "https://www.example.com/aas/" + assetIdShort;
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasId);
        var base64EncodedAssetId = Base64StringDeAndEncoder.EncodeTo64(assetId);

        var blueprintIds = new List<string> { "blueprint1", "blueprint2" };
        var data = new JObject();
        var language = "en";
        var submodelResults = new List<AasGeneratorResult>
        {
            new AasGeneratorResult { Success = true, BlueprintId = "blueprint1", GeneratedSubmodelId = "submodel1" },
            new AasGeneratorResult { Success = true, BlueprintId = "blueprint2", GeneratedSubmodelId = "submodel2" }
        };

        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(
                It.IsAny<string>(), 
                It.IsAny<IEnumerable<string>>(), 
                It.IsAny<JObject>(), 
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(
                new AasIds(assetId, assetIdShort, aasId, aasIdShort), 
                AasCreationStatus.Created,
                submodelResults));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest
        {
            BlueprintsIds = blueprintIds,
            Data = data,
            Language = language
        };

        // ACT 
        var result = await controller.CreateAas(assetIdShort, request);

        // ASSERT
        Assert.IsInstanceOf<ActionResult<CreateAasResponse>>(result);
        Assert.IsInstanceOf<OkObjectResult>(result.Result);
        var actionResult = result.Result as OkObjectResult;
        actionResult.Should().NotBeNull();
        actionResult?.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = actionResult?.Value as CreateAasResponse;
        response.Should().NotBeNull();
        response?.AssetId.Should().Be(assetId);
        response?.AasId.Should().Be(aasId);
        response?.SubmodelResults.Should().HaveCount(2);
        response?.SubmodelResults.All(r => r.Success).Should().BeTrue();
    }

    [Test]
    public async Task CreateAas_SubmodelGenerationFails_ReturnStatus400()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var assetId = "https://www.example.com/" + assetIdShort;
        var aasIdShort = "aas_" + assetIdShort;
        var aasId = "https://www.example.com/aas/" + assetIdShort;

        var blueprintIds = new List<string> { "blueprint1" };
        var data = new JObject();
        var language = "en";
        var submodelResults = new List<AasGeneratorResult>
        {
            new AasGeneratorResult { Success = false, BlueprintId = "blueprint1", Message = "Failed to generate submodel" }
        };

        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(
                It.IsAny<string>(), 
                It.IsAny<IEnumerable<string>>(), 
                It.IsAny<JObject>(), 
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(
                new AasIds(assetId, assetIdShort, aasId, aasIdShort), 
                AasCreationStatus.UnknownError,
                submodelResults,
                "Submodel generation failed. AAS was deleted."));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest
        {
            BlueprintsIds = blueprintIds,
            Data = data,
            Language = language
        };

        // ACT 
        var result = await controller.CreateAas(assetIdShort, request);

        // ASSERT
        Assert.IsInstanceOf<ActionResult<CreateAasResponse>>(result);
        Assert.IsInstanceOf<BadRequestObjectResult>(result.Result);
        var actionResult = result.Result as BadRequestObjectResult;
        actionResult.Should().NotBeNull();
        actionResult?.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var response = actionResult?.Value as CreateAasResponse;
        response.Should().NotBeNull();
        response?.SubmodelResults.Should().HaveCount(1);
        response?.SubmodelResults.First().Success.Should().BeFalse();
    }
}
