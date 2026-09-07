using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MnestixApi.Controllers;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AddDataToAas;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Core.Tests.Controllers;

public class DataIngestControllerTests
{
    private Mock<IAasGenerator> _aasGeneratorMock = null!;
    private DataIngestController _controller = null!;

    private static readonly AddDataToAasRequest DefaultRequest = new()
    {
        BlueprintsIds = ["urn:smtemplate:Test"],
        Data = new JObject()
    };

    [SetUp]
    public void SetUp()
    {
        _aasGeneratorMock = new Mock<IAasGenerator>();
        _controller = new DataIngestController(_aasGeneratorMock.Object, Mock.Of<ILogger<DataIngestController>>());
    }

    [Test]
    public async Task AddDataToAas_WhenBlueprintValidationFails_Returns500()
    {
        // ARRANGE
        var validationErrors = new List<BlueprintValidationError>
        {
            new(BlueprintValidationRule.EmptyMappingExpression, "submodelElements[0]", "Mapping expression is empty.")
        };
        var result = new AasGeneratorResult
        {
            BlueprintId = "urn:smtemplate:Test",
            Success = false,
            Error = new AasGeneratorErrorDto(
                AasGeneratorErrorCode.BlueprintValidationFailed,
                "Blueprint validation failed.",
                new ValidationErrorContext(validationErrors))
        };

        _aasGeneratorMock
            .Setup(x => x.AddDataToAasAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync([result]);

        // ACT
        var actionResult = await _controller.AddDataToAas("dGVzdA==", DefaultRequest);

        // ASSERT
        var statusCodeResult = actionResult as ObjectResult;
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Test]
    public async Task AddDataToAas_WhenRepositoryOperationFails_Returns400()
    {
        // ARRANGE
        var result = new AasGeneratorResult
        {
            BlueprintId = "urn:smtemplate:Test",
            Success = false,
            Error = new AasGeneratorErrorDto(AasGeneratorErrorCode.RepositoryOperationFailed, "Repository unavailable.", null)
        };

        _aasGeneratorMock
            .Setup(x => x.AddDataToAasAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync([result]);

        // ACT
        var actionResult = await _controller.AddDataToAas("dGVzdA==", DefaultRequest);

        // ASSERT
        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task AddDataToAas_WhenGenerationSucceeds_Returns200()
    {
        // ARRANGE
        var result = new AasGeneratorResult
        {
            BlueprintId = "urn:smtemplate:Test",
            Success = true,
            GeneratedSubmodelId = "urn:submodel:new"
        };

        _aasGeneratorMock
            .Setup(x => x.AddDataToAasAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync([result]);

        // ACT
        var actionResult = await _controller.AddDataToAas("dGVzdA==", DefaultRequest);

        // ASSERT
        actionResult.Should().BeOfType<OkObjectResult>();
    }
}
