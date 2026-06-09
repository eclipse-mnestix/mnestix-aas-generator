using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MnestixApi.ApiKeyAuthorization;
using MnestixApi.Options;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixApi.Controllers.deprecated;

/// <summary>
/// This controller duplicates an endpoint of the TemplateController to allow clients to authenticate via ApiKey
/// instead of calling AzureAd for an AccessToken.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[ApiKey]
public class DefaultTemplatesController : ControllerBase
{
    private readonly ILogger<DefaultTemplatesController> _logger;
    private readonly ITemplateCreator _defaultTemplateSubmodelCreator;
    private readonly bool _isTemplatesApiConfigured;

    /// <inheritdoc />
    public DefaultTemplatesController(ILogger<DefaultTemplatesController> logger, ITemplateCreator defaultTemplateSubmodelCreator, IOptions<ConfigurationOptions> configurationOptions)
    {
        _logger = logger;
        _defaultTemplateSubmodelCreator = defaultTemplateSubmodelCreator;
        _isTemplatesApiConfigured = !string.IsNullOrEmpty(configurationOptions.Value.SubmodelTemplatesApiUrl);
    }

    /// <summary>
    /// Creates a new custom template in the custom templates AAS.
    /// Submodel Id needs to be unique and present in JSON body.
    /// </summary>
    /// <param name="defaultSubmodelTemplate">The submodel template to add as json.</param>
    [HttpPost]
    public async Task<ActionResult> AddDefaultSubmodelTemplate([FromBody] object defaultSubmodelTemplate)
    {

        if (_isTemplatesApiConfigured)
        {
            return Problem(
                title: "Endpoint disabled",
                detail: "SubmodelTemplatesApiUrl is configured. Use the remote templates API instead.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        try
        {
            var defaultSubmodelString = defaultSubmodelTemplate.ToString();
            _logger.LogInformation("AddDefaultSubmodelTemplate : submodelId = {SubmodelId}",
                JToken.FromObject(defaultSubmodelTemplate)["id"]);
            _logger.LogTrace("defaultSubmodel = {DefaultSubmodelString}", defaultSubmodelString);

            Debug.Assert(defaultSubmodelString != null, nameof(defaultSubmodelString) + " != null");

            await _defaultTemplateSubmodelCreator.AddNewSubmodelInTemplateAasAsync(defaultSubmodelString);
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.LogError("Could not add default submodel template ... Error: {E}", e.Message);
            return BadRequest();
        }
    }
}