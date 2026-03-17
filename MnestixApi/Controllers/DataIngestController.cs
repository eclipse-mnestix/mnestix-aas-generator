using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AddDataToAas;
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

        var results = await _AasGenerator.AddDataToAasAsync(base64EncodedAasId, requestBody.BlueprintsIds, requestBody.Data, requestBody.Language, requestBody.Debug);
        var responseBody = new AddDataToAasResponse
        {
            Results = results
        };
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
