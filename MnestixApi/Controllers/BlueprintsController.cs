using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using MnestixApi.Controllers.deprecated;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixApi.Controllers;

/// <summary>
/// CRUD Operations for Blueprints (formerly known as CustomTemplates).
/// </summary>
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
[RequiredScope("admin.write")]
public class BlueprintsController : ControllerBase
{
    private readonly ILogger<BlueprintsController> _logger;
    private readonly IBlueprintProvider _customTemplateSubmodelsProvider;
    private readonly IBlueprintCreator _customTemplateSubmodelCreator;
    private readonly IBlueprintValidator _blueprintValidator;


    /// <inheritdoc />
    public BlueprintsController(ILogger<BlueprintsController> logger,
        IBlueprintCreator customTemplateSubmodelCreator,
        IBlueprintProvider customTemplateSubmodelsProvider,
        IBlueprintValidator blueprintValidator)
    {
        _logger = logger;
        _customTemplateSubmodelsProvider = customTemplateSubmodelsProvider;
        _customTemplateSubmodelCreator = customTemplateSubmodelCreator;
        _blueprintValidator = blueprintValidator;
    }

    /// <summary>
    /// Returns all blueprints.
    /// This endpoint uses the template transformer to ensure the returned submodels are standard conform. 
    /// </summary>
    /// <returns>Json which contains all blueprints.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetAllBlueprints()
    {
        try
        {
            _logger.LogInformation("GetAllBlueprints");
            var customSubmodels = await _customTemplateSubmodelsProvider.GetAllBlueprintsAsync();
            return Ok(customSubmodels);
        }
        catch (Exception e)
        {
            _logger.LogError("Could not get all blueprints. Error: {Message}", e.Message);
            return BadRequest(e);
        }
    }

    /// <summary>
    /// Returns the blueprint with the specified shortId.
    /// </summary>
    /// <returns>Json which contains the blueprint</returns>
    [HttpGet("{base64EncodedBlueprintId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetBlueprintById(string base64EncodedBlueprintId)
    {
        try
        {
            _logger.LogInformation("GetBlueprintById");
            var blueprint =
                await _customTemplateSubmodelsProvider.GetBlueprintAsync(base64EncodedBlueprintId);
            return Ok(blueprint);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not get blueprint with shortId {ShortId}", base64EncodedBlueprintId);
            return BadRequest();
        }
    }
    
    /// <summary>
    /// Creates a new blueprint of the given submodel semantic id. 
    /// </summary>
    /// <param name="blueprint">The blueprint as json string</param>
    /// <returns>The identifier of the new created blueprint.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CreateBlueprint([FromBody] object blueprint)
    {
        try
        {
            var defaultSubmodelString = blueprint.ToString();
            _logger.LogInformation("CreateCustomSubmodel : defaultSubmodel= {DefaultSubmodelString}",
                defaultSubmodelString);

            Debug.Assert(defaultSubmodelString != null, nameof(defaultSubmodelString) + " != null");

            var json = JObject.Parse(defaultSubmodelString);
            var validationErrors = _blueprintValidator.Validate(json);
            if (validationErrors.Count > 0)
            {
                return UnprocessableEntity(new { errors = validationErrors });
            }

            var submodelIdentifier =
                await _customTemplateSubmodelCreator.CreateNewSubmodelInBlueprintAasAsync(defaultSubmodelString);
            _logger.LogInformation("... Custom submodel created. Return new submodelIdentifier {SubmodelIdentifier}",
                submodelIdentifier);
            return Ok(submodelIdentifier);
        }
        catch (RepoProxyException ex) when (ex.StatusCode.HasValue)
        {
            _logger.LogError(ex, "Repository rejected blueprint. Status: {StatusCode}, Body: {Body}", ex.StatusCode, ex.ResponseBody);
            return new ContentResult
            {
                StatusCode = (int)ex.StatusCode.Value,
                Content = ex.ResponseBody,
                ContentType = "application/json"
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Could not create custom submodel.. Error: {E}", e.Message);
            return BadRequest();
        }
    }
    
    /// <summary>
    /// Updates a blueprint. 
    /// </summary>
    /// <param name="blueprint">The blueprint to update as json string</param>
    /// <param name="submodelId">The id of the submodel</param>
    [HttpPost("{submodelId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateBlueprint([FromBody] object blueprint,
        [FromRoute] string submodelId)
    {
        try
        {
            var blueprintAsString = blueprint.ToString();
            _logger.LogInformation("UpdateBlueprint (submodelId: {SubmodelId})", submodelId);
            _logger.LogTrace("blueprint= {CustomSubmodelString}", blueprintAsString);

            Debug.Assert(blueprintAsString != null, nameof(blueprintAsString) + " != null");

            var json = JObject.Parse(blueprintAsString);
            var validationErrors = _blueprintValidator.Validate(json);
            if (validationErrors.Count > 0)
            {
                return UnprocessableEntity(new { errors = validationErrors });
            }

            await _customTemplateSubmodelCreator.UpdateSubmodelInBlueprintAasAsync(blueprintAsString,
                submodelId);
            _logger.LogInformation("... blueprint updated");
            return NoContent();
        }
        catch (RepoProxyException ex) when (ex.StatusCode.HasValue)
        {
            _logger.LogError(ex, "Repository rejected blueprint update. Status: {StatusCode}, Body: {Body}", ex.StatusCode, ex.ResponseBody);
            return new ContentResult
            {
                StatusCode = (int)ex.StatusCode.Value,
                Content = ex.ResponseBody,
                ContentType = "application/json"
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Could not update blueprint.. Error: {E}", e.Message);
            return BadRequest();
        }
    }
    
    /// <summary>
    /// Deletes a blueprint identified by the base64-encoded template ID.
    /// </summary>
    /// <param name="base64EncodedBlueprintId">
    /// A base64-encoded string representing the unique identifier of the blueprint to be deleted.
    /// </param>
    /// <returns>
    /// Returns 204 No Content if the deletion was successful, 
    /// 404 Not Found if the specified template does not exist, 
    /// or 400 Bad Request if the input ID is invalid.
    /// </returns>
    [HttpDelete("{base64EncodedBlueprintId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> DeleteBlueprint(string base64EncodedBlueprintId)
    {
        try
        {
            _logger.LogInformation($"DeleteBlueprint {base64EncodedBlueprintId}");
            await _customTemplateSubmodelCreator.DeleteSubmodelInBlueprintAasAsync(base64EncodedBlueprintId);
           
            return NoContent();
        }
        catch (RepoProxyException repoException)
        {
            if(repoException.ErrorCode == ErrorCodes.CouldNotFind) return NotFound(repoException?.InnerException?.Message);

            return BadRequest();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not delete blueprint with Id {ShortId}", base64EncodedBlueprintId);

            return BadRequest();
        }
    }
}