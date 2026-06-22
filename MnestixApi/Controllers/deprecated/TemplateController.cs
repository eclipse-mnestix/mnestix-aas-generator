using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixApi.Controllers.deprecated;

/// <summary>
/// This controller provides endpoints to add or update submodel templates.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
[RequiredScope("admin.write")]
public class TemplateController : ControllerBase
{
    private readonly ILogger<TemplateController> _logger;
    private readonly IBlueprintCreator _customTemplateSubmodelCreator;
    private readonly IBlueprintProvider _customTemplateSubmodelsProvider;
    private readonly ITemplateProvider _defaultTemplateSubmodelProvider;
    private readonly ITemplateCreator _defaultTemplateSubmodelCreator;

    /// <inheritdoc />
    public TemplateController(ILogger<TemplateController> logger,
        IBlueprintCreator customTemplateSubmodelCreator,
        IBlueprintProvider customTemplateSubmodelsProvider,
        ITemplateProvider defaultTemplateSubmodelProvider,
        ITemplateCreator defaultTemplateSubmodelCreator)
    {
        _logger = logger;
        _customTemplateSubmodelCreator = customTemplateSubmodelCreator;
        _customTemplateSubmodelsProvider = customTemplateSubmodelsProvider;
        _defaultTemplateSubmodelProvider = defaultTemplateSubmodelProvider;
        _defaultTemplateSubmodelCreator = defaultTemplateSubmodelCreator;
    }

    /// <summary>
    /// ONLY FOR INTERNAL USAGE. BearerToken needed.
    /// Creates a new custom template in the custom templates AAS of the given submodel semantic id. 
    /// </summary>
    /// <param name="defaultSubmodel">The default submodel as json string</param>
    /// <returns>The identifier of the new created submodel in the custom templates AAS.</returns>
    [HttpPost("createCustomSubmodel")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateCustomSubmodel([FromBody] object defaultSubmodel)
    {
        try
        {
            var defaultSubmodelString = defaultSubmodel.ToString();
            _logger.LogInformation("CreateCustomSubmodel : defaultSubmodel= {DefaultSubmodelString}",
                defaultSubmodelString);

            Debug.Assert(defaultSubmodelString != null, nameof(defaultSubmodelString) + " != null");

            var submodelIdentifier =
                await _customTemplateSubmodelCreator.CreateNewSubmodelInBlueprintAasAsync(defaultSubmodelString);
            _logger.LogInformation("... Custom submodel created. Return new submodelIdentifier {SubmodelIdentifier}",
                submodelIdentifier);
            return Ok(submodelIdentifier);
        }
        catch (Exception e)
        {
            _logger.LogError("Could not create custom submodel.. Error: {E}", e.Message);
            return BadRequest();
        }
    }

    /// <summary>
    /// ONLY FOR INTERNAL USAGE. BearerToken needed.
    /// Updates a custom template in the custom templates AAS. 
    /// </summary>
    /// <param name="customSubmodel">The submodel to update as json string</param>
    /// <param name="submodelId">The id of the submodel</param>
    [HttpPost("updateCustomSubmodel/{submodelId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateCustomSubmodel([FromBody] object customSubmodel,
        [FromRoute] string submodelId)
    {
        try
        {
            var customSubmodelString = customSubmodel.ToString();
            _logger.LogInformation("UpdateCustomSubmodel (submodelId: {SubmodelId})", submodelId);
            _logger.LogTrace("customSubmodel= {CustomSubmodelString}", customSubmodelString);

            Debug.Assert(customSubmodelString != null, nameof(customSubmodelString) + " != null");

            await _customTemplateSubmodelCreator.UpdateSubmodelInBlueprintAasAsync(customSubmodelString,
                submodelId);
            _logger.LogInformation("... Custom submodel updated");
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.LogError("Could not update custom submodel.. Error: {E}", e.Message);
            return BadRequest();
        }
    }

    /// <summary>
    /// ONLY FOR INTERNAL USAGE. BearerToken needed.
    /// Creates a new custom template in the custom templates AAS. 
    /// </summary>
    /// <returns>The identifier of the new created submodel in the custom templates AAS.</returns>
    [HttpPost("addDefaultSubmodel")]
    [ProducesResponseType(StatusCodes.Status204NoContent, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddDefaultSubmodel([FromBody] object defaultSubmodel)
    {
        try
        {
            var defaultSubmodelString = defaultSubmodel.ToString();
            _logger.LogInformation("AddDefaultSubmodel : submodelId = {SubmodelId}",
                JToken.FromObject(defaultSubmodel)["id"]);
            _logger.LogTrace("defaultSubmodel = {DefaultSubmodelString}", defaultSubmodelString);

            Debug.Assert(defaultSubmodelString != null, nameof(defaultSubmodelString) + " != null");

            await _defaultTemplateSubmodelCreator.AddNewSubmodelInTemplateAasAsync(defaultSubmodelString);
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.LogError("Could not add default submodel.. Error: {E}", e.Message);
            return BadRequest();
        }
    }


    /// <summary>
    /// ONLY FOR INTERNAL USAGE. BearerToken needed.
    /// Returns all submodel templates from the custom templates AAS.
    /// 
    /// This endpoint uses the template transformer to ensure the returned submodels are standard conform. 
    /// </summary>
    /// <returns>Json which contains all custom submodels.</returns>
    [HttpGet("allCustomSubmodels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetAllCustomSubmodels()
    {
        try
        {
            _logger.LogInformation("GetAllCustomSubmodels");
            var customSubmodels = await _customTemplateSubmodelsProvider.GetAllBlueprintsAsync();
            return Ok(customSubmodels);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not get all custom submodels...");
            return NotFound(e);
        }
    }

    /// <summary>
    /// ONLY FOR INTERNAL USAGE. BearerToken needed.
    /// Returns one submodel templates from the custom templates AAS.
    /// 
    /// This endpoint uses the template transformer to ensure the returned submodels are standard conform. 
    /// </summary>
    /// <returns>Json which contains all custom submodels.</returns>
    [HttpGet("customSubmodel/{submodelIdShort}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetCustomSubmodel([FromRoute] string submodelIdShort)
    {
        try
        {
            _logger.LogInformation("GetCustomSubmodel - submodelIdShort: {SubmodelIdShort}", submodelIdShort);
            var customSubmodel = await _customTemplateSubmodelsProvider.GetBlueprintAsync(submodelIdShort);
            return Ok(customSubmodel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not get all custom submodels...");
            return NotFound(e);
        }
    }

    /// <summary>
    /// ONLY FOR INTERNAL USAGE. BearerToken needed.
    /// Returns all default submodel templates from the default templates AAS.
    /// 
    /// This endpoint uses the template transformer to ensure the returned submodels are standard conform. 
    /// </summary>
    /// <returns>Json which contains all default submodels.</returns>
    [HttpGet("allDefaultSubmodels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetAllDefaultSubmodels()
    {
        try
        {
            _logger.LogInformation($"GetAllDefaultSubmodels...");
            var allDefaultTemplateSubmodels =
                await _defaultTemplateSubmodelProvider.GetAllTemplateSubmodelsAsync();

            _logger.LogTrace("... GetAllDefaultSubmodels return: {AllDefaultTemplateSubmodels}",
                allDefaultTemplateSubmodels);

            return Ok(allDefaultTemplateSubmodels);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not get all custom submodels..");
            return NotFound(e);
        }
    }

    /// <summary>
    /// Deletes a custom submodel identified by the base64-encoded template ID.
    /// </summary>
    /// <param name="base64EncodedCustomTemplateId">
    /// A base64-encoded string representing the unique identifier of the custom template to be deleted.
    /// </param>
    /// <returns>
    /// Returns 204 No Content if the deletion was successful, 
    /// 404 Not Found if the specified template does not exist, 
    /// or 400 Bad Request if the input ID is invalid.
    /// </returns>
    [HttpDelete("{base64EncodedCustomTemplateId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> DeleteCustomSubmodel(string base64EncodedCustomTemplateId)
    {
        try
        {
            _logger.LogInformation($"DeleteCustomSubmodel {base64EncodedCustomTemplateId}");
            await _customTemplateSubmodelCreator.DeleteSubmodelInBlueprintAasAsync(base64EncodedCustomTemplateId);
           
            return NoContent();
        }
        catch (RepoProxyException repoException)
        {
            if(repoException.ErrorCode == ErrorCodes.CouldNotFind) return NotFound(repoException?.InnerException?.Message);

            return BadRequest();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not delete custom submodel with Id {ShortId}", base64EncodedCustomTemplateId);

            return BadRequest();
        }
    }
}