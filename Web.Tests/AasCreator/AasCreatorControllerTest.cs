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
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
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
        var previous = JObject.Parse("{\"id\":\"old-shell\",\"idShort\":\"old\"}");
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
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
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task CreateAas_ForwardsOverwriteQueryParamToService(bool overwrite)
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT
        await controller.CreateAas(assetIdShort, overwrite, null);

        // ASSERT
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(assetIdShort, It.IsAny<CreateAasParameters?>(), overwrite), Times.Once);
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
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
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
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.GenerationFailed, submodelResults, errorMessage: "Submodel generation failed. No AAS was created."));
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

    [Test]
    public async Task CreateAas_MissingDataOrLanguage_ReturnsStatus400()
    {
        // ARRANGE — generation/validation guard failed, no submodel results produced
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.GenerationFailed, Enumerable.Empty<AasGeneratorResult>(), errorMessage: "BlueprintsIds provided but Data or Language is missing."));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { BlueprintsIds = new[] { "blueprint1" } };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as BadRequestObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        objectResult.Value.Should().Be("BlueprintsIds provided but Data or Language is missing.");
    }

    [Test]
    public async Task CreateAas_InfrastructureFailure_ReturnsStatus500()
    {
        // ARRANGE — infra/persistence failure (e.g. submodel/shell POST threw)
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.UnknownError, Enumerable.Empty<AasGeneratorResult>(), errorMessage: "Failed to create AAS shell: boom"));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { BlueprintsIds = new[] { "blueprint1" }, Data = new JObject(), Language = "en" };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        objectResult.Value.Should().Be("Failed to create AAS shell: boom");
    }

    [Test]
    public async Task CreateAas_WithThumbnailWithoutPath_ReturnsStatus400()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { DefaultThumbnail = new DefaultThumbnail { Path = "" } };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as BadRequestObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(
            It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task CreateAas_WithValidThumbnail_PassesThumbnailToService()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var thumbnail = new DefaultThumbnail { Path = "https://example.com/logo.png", ContentType = "image/png" };
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { DefaultThumbnail = thumbnail };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(
            assetIdShort,
            It.Is<CreateAasParameters?>(p => p != null
                && p.Metadata != null
                && p.Metadata.DefaultThumbnail != null
                && p.Metadata.DefaultThumbnail.Path == thumbnail.Path
                && p.Metadata.DefaultThumbnail.ContentType == thumbnail.ContentType),
            false), Times.Once);
    }

    [Test]
    public async Task CreateAas_WithTypeAssetKind_PassesTypeToService()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { AssetKind = AssetKind.Type };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(
            assetIdShort,
            It.Is<CreateAasParameters?>(p => p != null && p.Metadata != null && p.Metadata.AssetKind == AssetKind.Type),
            false), Times.Once);
    }

    [Test]
    public async Task CreateAas_WithNotApplicableAssetKind_PassesNotApplicableToService()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { AssetKind = AssetKind.NotApplicable };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(
            assetIdShort,
            It.Is<CreateAasParameters?>(p => p != null && p.Metadata != null && p.Metadata.AssetKind == AssetKind.NotApplicable),
            false), Times.Once);
    }

    [Test]
    public async Task CreateAas_WithoutAssetKind_DefaultsToInstance()
    {
        // ARRANGE
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        var request = new CreateAasRequest { };

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, request);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(
            assetIdShort,
            It.Is<CreateAasParameters?>(p => p != null && p.Metadata != null && p.Metadata.AssetKind == AssetKind.Instance),
            false), Times.Once);
    }

    [Test]
    public async Task CreateAas_WithoutRequestBody_DefaultsToInstance()
    {
        // ARRANGE - backward compatibility test: no request body at all
        var assetIdShort = Guid.NewGuid().ToString();
        var ids = Ids(assetIdShort);
        var mockLogger = new Mock<ILogger<AasCreatorController>>();
        var mockService = new Mock<IAasCreatorService>();
        mockService.Setup(s => s.CreateAasWithSubmodelsAsync(It.IsAny<string>(), It.IsAny<CreateAasParameters?>(), It.IsAny<bool>()))
            .ReturnsAsync(new AasCreationWithSubmodelsResult(ids, AasCreationStatus.Created, Enumerable.Empty<AasGeneratorResult>()));
        var controller = new AasCreatorController(mockLogger.Object, mockService.Object);

        // ACT
        var result = await controller.CreateAas(assetIdShort, false, null);

        // ASSERT
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        mockService.Verify(s => s.CreateAasWithSubmodelsAsync(assetIdShort, null, false), Times.Once);
    }
}
