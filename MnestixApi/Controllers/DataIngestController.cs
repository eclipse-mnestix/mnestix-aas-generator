using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AddDataToAas;
using MnestixCore.TemplateBuilder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MnestixApi.Controllers;

/// <summary>
/// This controller provides endpoints to add mass data.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class DataIngestController : ControllerBase
{
    private readonly IAasGenerator _AasGenerator;
    private readonly ILogger<DataIngestController> _logger;

    /// <inheritdoc />
    public DataIngestController(IAasGenerator AasGenerator, ILogger<DataIngestController> logger)
    {
        _AasGenerator = AasGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Takes the blueprint with given blueprintIds and maps the data from the given data json into them.
    /// After that, it will store the submodels into the shell with given aasId with its submodel short id.
    /// </summary>
    /// <param name="base64EncodedAasId">The base64UrlEncoded aasId of the shell where the submodel will be stored in.</param>
    /// <param name="requestBody">The language (e.g.: 'de' or 'en'), a list of blueprint ids and a json with the data for the new submodels.
    /// If you do not have any mapping info defined in the referenced submodel, use {} as data json.</param>
    /// <returns>a list of results for each given blueprint ids</returns>
    [ProducesResponseType(typeof(AddDataToAasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [HttpPost("{base64EncodedAasId}")]
    public async Task<ActionResult> AddDataToAas(string base64EncodedAasId, [FromBody] AddDataToAasRequest requestBody)
    {
        _logger.LogInformation("invoked DataIngest/{AasId}/ with blueprintIds: {BlueprintIds}", base64EncodedAasId, string.Join(", ", requestBody.BlueprintsIds));

        var results = (await _AasGenerator.AddDataToAasAsync(base64EncodedAasId, requestBody.BlueprintsIds, requestBody.Data, requestBody.Language, requestBody.Debug)).ToList();
        var responseBody = new AddDataToAasResponse
        {
            Results = results
        };

        // At the beginning of the generation pipeline we validate the blueprint if it was uploaded
        // If this validation fails, the blueprint either comes from an older AAS Generator version without the new validation rules or it was externally modified and is now in an invalid state. 
        // We return a 500 Internal Server Error in this case, because the blueprint is not in a valid state for the AAS Generator to process it, even though the request itself is valid.
        var validationErrors = results
            .Where(r => r.ValidationErrors != null)
            .SelectMany(r => r.ValidationErrors!)
            .ToList();

        if (validationErrors.Count > 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { errors = validationErrors, results = responseBody.Results });
        }

        if (results.FirstOrDefault()?.Success == true)
        {
            return Ok(responseBody);
        }
        else
        {
            return BadRequest(responseBody);
        }
    }
}
