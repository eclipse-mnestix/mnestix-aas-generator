using Microsoft.AspNetCore.Mvc;
using MnestixApi.ApiKeyAuthorization;
using MnestixCore.Shared;
using MnestixCore.TemplateBuilder.Interfaces;

namespace MnestixApi.Controllers.deprecated;

/// <summary>
/// This controller duplicates an endpoint of the TemplateController to allow clients to authenticate via ApiKey
/// instead of calling AzureAd for an AccessToken.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[ApiKey]
public class CustomTemplatesController : ControllerBase
{
    private readonly ILogger<CustomTemplatesController> _logger;
    private readonly IBlueprintProvider _customTemplateSubmodelsProvider;

    /// <inheritdoc />
    public CustomTemplatesController(ILogger<CustomTemplatesController> logger, IBlueprintProvider customTemplateSubmodelsProvider)
    {
        _logger = logger;
        _customTemplateSubmodelsProvider = customTemplateSubmodelsProvider;
    }

    /// <summary>
    /// Returns all submodel templates from the custom templates AAS.
    /// 
    /// This endpoint uses the template transformer to ensure the returned submodels are standard conform. 
    /// </summary>
    /// <returns>Json which contains all custom submodels.</returns>
    [HttpGet]
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
            _logger.LogError("Could not get all custom submodels. Error: {Message}", e.Message);
            return BadRequest(e);
        }
    }

    /// <summary>
    /// Returns the submodel template from the custom templates AAS with the specified shortId.
    /// </summary>
    /// <returns>Json which contains the custom submodel</returns>
    [HttpGet("{base64EncodedCustomTemplateId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetCustomSubmodel(string base64EncodedCustomTemplateId)
    {
        try
        {
            _logger.LogInformation("GetCustomSubmodel");
            // The route carries a Base64Url-encoded id (unchanged REST contract); decode before
            // calling the provider, which re-encodes the consumer-facing id internally.
            var customTemplateId = Base64StringDeAndEncoder.DecodeFrom64(base64EncodedCustomTemplateId);
            var customSubmodel = await _customTemplateSubmodelsProvider.GetBlueprintAsync(customTemplateId);
            return Ok(customSubmodel);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not get custom submodel with shortId {ShortId}", base64EncodedCustomTemplateId);
            return BadRequest();
        }
    }
}