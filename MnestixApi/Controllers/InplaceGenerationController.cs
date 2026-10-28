using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Dtos.InplaceGeneration;
using MnestixCore.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MnestixApi.Controllers;

/// <summary>
/// This controller provides an endpoint to generate submodels from blueprints without persisting them.
/// </summary>
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class InplaceGenerationController : ControllerBase
{
    private readonly IAasGenerator _aasGenerator;
    private readonly ILogger<InplaceGenerationController> _logger;
    private readonly AasGeneratorOptions _aasGeneratorOptions;

    /// <inheritdoc />
    public InplaceGenerationController(IAasGenerator aasGenerator, ILogger<InplaceGenerationController> logger, IOptions<AasGeneratorOptions> aasGeneratorOptions)
    {
        _aasGenerator = aasGenerator;
        _logger = logger;
        _aasGeneratorOptions = aasGeneratorOptions.Value;
    }

    /// <summary>
    /// Takes the blueprints with given blueprintIds and maps the data from the given data json into them, the same
    /// way AasCreation/DataIngest do. Unlike those endpoints, the generated submodels are returned in the response
    /// body instead of being posted to the repository - the caller is responsible for persisting them.
    /// </summary>
    /// <param name="requestBody">The language (e.g.: 'de' or 'en'), a list of blueprint ids and a json with the data for the new submodels.</param>
    /// <returns>a list of results for each given blueprint id, including the generated submodel body</returns>
    [ProducesResponseType(typeof(InplaceGenerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult> Generate([FromBody] InplaceGenerationRequest requestBody)
    {
        _logger.LogInformation("invoked InplaceGeneration with blueprintIds: {BlueprintIds}", string.Join(", ", requestBody.BlueprintsIds));

        if (requestBody.BlueprintsIds.Count() > _aasGeneratorOptions.MaxPayloadLimit)
        {
            return BadRequest(
                new AasGeneratorErrorDto(
                    AasGeneratorErrorCode.InvalidInput,
                    $"Too many blueprint IDs. Up to {_aasGeneratorOptions.MaxPayloadLimit} blueprints can be built at once.",
                    null
                )
            );
        }

        var results = new List<InplaceGenerationResult>();
        foreach (var blueprintId in requestBody.BlueprintsIds)
        {
            var built = await _aasGenerator.BuildSubmodelAsync(blueprintId, requestBody.Data, requestBody.Language, requestBody.Debug);
            results.Add(new InplaceGenerationResult
            {
                BlueprintId = built.Result.BlueprintId,
                Success = built.Result.Success,
                GeneratedSubmodelId = built.Result.GeneratedSubmodelId,
                Submodel = built.Instance,
                Error = built.Result.Error,
                Logs = built.Result.Logs
            });
        }

        var responseBody = new InplaceGenerationResponse { Results = results };

        // At the beginning of the generation pipeline we validate the blueprint if it was uploaded
        // If this validation fails, the blueprint either comes from an older AAS Generator version without the new validation rules or it was externally modified and is now in an invalid state.
        // We return a 500 Internal Server Error in this case, because the blueprint is not in a valid state for the AAS Generator to process it, even though the request itself is valid.
        var validationErrors = results
            .Select(r => r.Error?.Context as ValidationErrorContext)
            .Where(c => c != null)
            .SelectMany(c => c!.Errors)
            .ToList();

        if (validationErrors.Count > 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { errors = validationErrors, results = responseBody.Results });
        }

        if (results.All(r => r.Success))
        {
            return Ok(responseBody);
        }

        return BadRequest(responseBody);
    }
}
