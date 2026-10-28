using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MnestixApi.Controllers;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.InplaceGeneration;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using Moq;
using Newtonsoft.Json.Linq;

namespace Web.Tests.Controllers;

public class InplaceGenerationControllerTests
{
    private Mock<IAasGenerator> _aasGeneratorMock = null!;
    private InplaceGenerationController _controller = null!;

    private static readonly InplaceGenerationRequest DefaultRequest = new()
    {
        BlueprintId = "urn:smtemplate:Test",
        Data = new JObject()
    };

    [SetUp]
    public void SetUp()
    {
        _aasGeneratorMock = new Mock<IAasGenerator>();
        _controller = new InplaceGenerationController(_aasGeneratorMock.Object, Mock.Of<ILogger<InplaceGenerationController>>());
    }

    [Test]
    public async Task Generate_WhenBuildSucceeds_Returns200WithEnvironmentContainingOnlySubmodel()
    {
        // ARRANGE
        var submodel = new JObject { ["id"] = "urn:submodel:new" };
        _aasGeneratorMock
            .Setup(x => x.BuildSubmodelAsync(It.IsAny<string>(), It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(new BuiltSubmodel
            {
                Instance = submodel,
                Result = new AasGeneratorResult { BlueprintId = "urn:smtemplate:Test", Success = true, GeneratedSubmodelId = "urn:submodel:new" }
            });

        // ACT
        var actionResult = await _controller.Generate(DefaultRequest);

        // ASSERT: nothing is posted to any repository, the environment only carries the submodel
        actionResult.Should().BeOfType<OkObjectResult>();
        var response = ((OkObjectResult)actionResult).Value as InplaceGenerationResponse;
        response!.AssetAdministrationShells.Should().BeEmpty();
        response.ConceptDescriptions.Should().BeEmpty();
        response.Submodels.Should().ContainSingle().Which.Should().BeSameAs(submodel);
        _aasGeneratorMock.Verify(
            x => x.BuildSubmodelAsync("urn:smtemplate:Test", It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Test]
    public async Task Generate_WhenBlueprintValidationFails_Returns500()
    {
        // ARRANGE
        var validationErrors = new List<BlueprintValidationError>
        {
            new(BlueprintValidationRule.EmptyMappingExpression, "submodelElements[0]", "Mapping expression is empty.")
        };

        _aasGeneratorMock
            .Setup(x => x.BuildSubmodelAsync(It.IsAny<string>(), It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(new BuiltSubmodel
            {
                Result = new AasGeneratorResult
                {
                    BlueprintId = "urn:smtemplate:Test",
                    Success = false,
                    Error = new AasGeneratorErrorDto(
                        AasGeneratorErrorCode.BlueprintValidationFailed,
                        "Blueprint validation failed.",
                        new ValidationErrorContext(validationErrors))
                }
            });

        // ACT
        var actionResult = await _controller.Generate(DefaultRequest);

        // ASSERT
        var statusCodeResult = actionResult as ObjectResult;
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Test]
    public async Task Generate_WhenBuildFails_Returns400()
    {
        // ARRANGE
        _aasGeneratorMock
            .Setup(x => x.BuildSubmodelAsync(It.IsAny<string>(), It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(new BuiltSubmodel
            {
                Result = new AasGeneratorResult
                {
                    BlueprintId = "urn:smtemplate:Test",
                    Success = false,
                    Error = new AasGeneratorErrorDto(AasGeneratorErrorCode.UnknownError, "Something went wrong.", null)
                }
            });

        // ACT
        var actionResult = await _controller.Generate(DefaultRequest);

        // ASSERT
        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }
}
