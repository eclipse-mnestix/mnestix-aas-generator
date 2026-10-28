using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.InplaceGeneration;
using MnestixCore.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MnestixApi.Controllers;

/// <summary>
/// This controller provides an endpoint to generate a submodel from a blueprint without persisting it.
/// </summary>
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class InplaceGenerationController : ControllerBase
{
    private readonly IAasGenerator _aasGenerator;
    private readonly ILogger<InplaceGenerationController> _logger;

    /// <inheritdoc />
    public InplaceGenerationController(IAasGenerator aasGenerator, ILogger<InplaceGenerationController> logger)
    {
        _aasGenerator = aasGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Takes the blueprint with given blueprintId and maps the data from the given data json into it, the same way
    /// AasCreation/DataIngest do. Unlike those endpoints, nothing is written to the repository - the generated
    /// submodel is returned as an AAS environment (with empty assetAdministrationShells and conceptDescriptions)
    /// and the caller is responsible for persisting it.
    /// </summary>
    /// <param name="requestBody">The language (e.g.: 'de' or 'en'), the blueprint id and a json with the data for the new submodel.</param>
    /// <returns>an AAS environment whose submodels field contains the generated submodel</returns>
    [ProducesResponseType(typeof(InplaceGenerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult> Generate([FromBody] InplaceGenerationRequest requestBody)
    {
        _logger.LogInformation("invoked InplaceGeneration with blueprintId: {BlueprintId}", requestBody.BlueprintId);

        var built = await _aasGenerator.BuildSubmodelAsync(requestBody.BlueprintId, requestBody.Data, requestBody.Language, requestBody.Debug);

        if (!built.Result.Success)
        {
            // At the beginning of the generation pipeline we validate the blueprint if it was uploaded
            // If this validation fails, the blueprint either comes from an older AAS Generator version without the new validation rules or it was externally modified and is now in an invalid state.
            // We return a 500 Internal Server Error in this case, because the blueprint is not in a valid state for the AAS Generator to process it, even though the request itself is valid.
            if (built.Result.Error?.Context is ValidationErrorContext validationErrorContext)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { errors = validationErrorContext.Errors, result = built.Result });
            }

            return BadRequest(built.Result);
        }

        var environment = new InplaceGenerationResponse
        {
            Submodels = [built.Instance!]
        };

        return Ok(environment);
    }
}
