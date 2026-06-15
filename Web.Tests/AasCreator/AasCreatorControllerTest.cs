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
    private static AasIds Ids(string assetIdShort) =>
        new("https://www.example.com/" + assetIdShort,
            assetIdShort,
            "https://www.example.com/aas/" + assetIdShort,
            "aas_" + assetIdShort);

    [Test]
    public async Task CreateAas_NewCreation_ReturnsStatus201()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), null, null, null, It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, null);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        var response = objectResult.Value as CreateAasResponse;
        response.Should().NotBeNull();
        response!.AasId.Should().Be(ids.aasId);
        response.PreviousAas.Should().BeNull();
    }

    [Test]
    public async Task CreateAas_Overwritten_ReturnsStatus200WithPreviousAas()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var previous = "{\"id\":\"old-shell\",\"idShort\":\"old\"}";
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), true))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Overwritten, Enumerable.Empty<AasGeneratorResult>(), "https://repo", previousAas: previous));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT
        var result = await controller.CreateAas(assetIdShort, true, new CreateAasRequest { BlueprintsIds = new[] { "bp1" }, Data = new JObject(), Language = "en" });

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = objectResult.Value as CreateAasResponse;
        response.Should().NotBeNull();
        response!.PreviousAas.Should().NotBeNull();
        response.PreviousAas!["id"]!.ToString().Should().Be("old-shell");
    }

    [Test]
    public async Task CreateAas_Conflict_ReturnsStatus409WithOrphanedSubmodelIds()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), false))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Conflict, Enumerable.Empty<AasGeneratorResult>(), errorMessage: "AAS already exists, use overwrite=true to replace", orphanedSubmodelIds: new[] { "sm1" }));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, new CreateAasRequest { BlueprintsIds = new[] { "bp1" }, Data = new JObject(), Language = "en" });

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var response = objectResult.Value as CreateAasConflictResponse;
        response.Should().NotBeNull();
        response!.OrphanedSubmodelIds.Should().Contain("sm1");
    }

    [Test]
    public async Task CreateAas_ForwardsOverwriteQueryParamToService()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), null, null, null, It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT
        await controller.CreateAas(assetIdShort, true, null);

        // ASSERT
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(assetIdShort, null, null, null, It.IsAny<bool>(), It.IsAny<string?>(), true), Times.Once);
    }

    [Test]
    public async Task CreateAas_WithSubmodels_ReturnsSubmodelResults()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var submodelResults = new List<AasGeneratorResult>
        {
            new() { Success = true, BlueprintId = "blueprint1", GeneratedSubmodelId = "submodel1" },
            new() { Success = true, BlueprintId = "blueprint2", GeneratedSubmodelId = "submodel2" }
        };
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, submodelResults));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { BlueprintsIds = new[] { "blueprint1", "blueprint2" }, Data = new JObject(), Language = "en" };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        var response = objectResult.Value as CreateAasResponse;
        response!.SubmodelResults.Should().HaveCount(2);
        response.SubmodelResults.All(r => r.Success).Should().BeTrue();
    }

    [Test]
    public async Task CreateAas_SubmodelGenerationFails_ReturnsStatus400()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var submodelResults = new List<AasGeneratorResult>
        {
            new() { Success = false, BlueprintId = "blueprint1", Message = "Failed to generate submodel" }
        };
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.UnknownError, submodelResults, errorMessage: "Submodel generation failed. No AAS was created."));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { BlueprintsIds = new[] { "blueprint1" }, Data = new JObject(), Language = "en" };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as BadRequestObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var response = objectResult.Value as CreateAasResponse;
        response!.SubmodelResults.Should().HaveCount(1);
        response.SubmodelResults.First().Success.Should().BeFalse();
    }
}
