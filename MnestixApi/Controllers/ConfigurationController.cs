using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MnestixCore.ConfigurationService.Interfaces;

namespace MnestixApi.Controllers;

/// <summary>
/// API controller for managing ID generation configuration settings.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConfigurationController"/> class.
/// </remarks>
/// <param name="configurationService">The configuration service handling ID generation settings.</param>
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class ConfigurationController(IConfigurationService configurationService) : ControllerBase
{
    /// <summary>
    /// Retrieves the current ID generation configuration settings.
    /// </summary>
    /// <returns>
    /// Returns <see cref="OkObjectResult"/> with the configuration settings if found,
    /// otherwise returns <see cref="NotFoundResult"/>.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult> GetIdConfiguration()
    {
        var settings = await configurationService.GetIdGenerationSettings();

        if (settings == null) return NotFound();

        return Ok(settings);
    }

    /// <summary>
    /// Applies a partial update to a specific ID generation configuration value.
    /// </summary>
    /// <param name="idShortPath">The path to the setting within the submodel elements.</param>
    /// <param name="value">The new value to apply.</param>
    /// <returns>
    /// Returns <see cref="NoContentResult"/> if the patch was successful,
    /// or <see cref="NotFoundResult"/> if the target setting could not be found or updated.
    /// </returns>
    [HttpPatch]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    public async Task<ActionResult> PatchIdConfiguration([FromQuery] string idShortPath, string value)
    {
        var result = await configurationService.PatchSingleIdGenerationSetting(idShortPath, value);

        if (result == false) return NotFound();

        return NoContent();
    }
}