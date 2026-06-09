using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MnestixApi.Controllers.deprecated;
using MnestixApi.Options;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixApi.Controllers;

/// <summary>
/// Templates Controller, handling CRUD operations for templates (formerly known as DefaultTemplates).
/// </summary>
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class TemplatesController: ControllerBase
{
    private readonly ILogger<TemplateController> _logger;
    private readonly ITemplateProvider _templateSubmodelProvider;
    private readonly ITemplateCreator _templateSubmodelCreator;
    private readonly bool _isTemplatesApiConfigured;

    /// <inheritdoc />
    public TemplatesController(ILogger<TemplateController> logger,
        ITemplateProvider templateSubmodelProvider,
        ITemplateCreator templateSubmodelCreator,
        IOptions<ConfigurationOptions> configurationOptions)
    {
        _logger = logger;
        _templateSubmodelProvider = templateSubmodelProvider;
        _templateSubmodelCreator = templateSubmodelCreator;
        _isTemplatesApiConfigured = !string.IsNullOrEmpty(configurationOptions.Value.SubmodelTemplatesApiUrl);
    }
    
    /// <summary>
    /// Creates a new template in the templates AAS when the local endpoint is enabled.
    /// </summary>
    /// <remarks>
    /// If <see cref="ConfigurationOptions.SubmodelTemplatesApiUrl"/> is configured, the endpoint returns
    /// <see cref="StatusCodes.Status403Forbidden"/> instructing clients to use the remote templates API instead.
    /// Otherwise, it logs the payload's <c>id</c> and forwards the body to
    /// <see cref="ITemplateCreator.AddNewSubmodelInTemplateAasAsync(string)"/>.
    /// </remarks>
    /// <returns>A <see cref="NoContentResult"/> when the template is added successfully, or the corresponding error response.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent, Type = typeof(string))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateTemplate([FromBody] object template)
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
            var templateAsString = template.ToString();
            _logger.LogInformation("CreateTemplate : submodelId = {SubmodelId}",
                JToken.FromObject(template)["id"]);
            _logger.LogTrace("template = {DefaultSubmodelString}", templateAsString);

            Debug.Assert(templateAsString != null, nameof(templateAsString) + " != null");

            await _templateSubmodelCreator.AddNewSubmodelInTemplateAasAsync(templateAsString);
            return NoContent();
        }
        catch (Exception e)
        {
            _logger.LogError("Could not add template.. Error: {E}", e.Message);
            return BadRequest();
        }
    }
    
    /// <summary>
    /// Returns all submodel templates from the templates AAS.
    /// 
    /// This endpoint uses the template transformer to ensure the returned submodels are standard conform. 
    /// </summary>
    /// <returns>Json which contains all template submodels.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GetAllTemplates()
    {
        try
        {
            _logger.LogInformation($"GetAllTemplates...");
            var allDefaultTemplateSubmodels =
                await _templateSubmodelProvider.GetAllTemplateSubmodelsAsync();

            _logger.LogTrace("... GetAllTemplates return: {AllDefaultTemplateSubmodels}",
                allDefaultTemplateSubmodels);

            return Ok(allDefaultTemplateSubmodels);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not get all templates..");
            return NotFound(e);
        }
    }
}