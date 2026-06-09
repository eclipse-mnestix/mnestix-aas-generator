using MnestixCore.AasCreator;
using MnestixCore.AasCreator.Interfaces;
using MnestixCore.Dtos;
using MnestixCore.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MnestixApi.Controllers;

/// <summary>
/// This controller provides endpoints to create AAS.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class AasCreatorController : ControllerBase
{
    private readonly IAasCreatorService _aasCreatorService;
    private readonly ILogger<AasCreatorController> _logger;

    /// <inheritdoc />
    public AasCreatorController(ILogger<AasCreatorController> logger, IAasCreatorService aasCreatorService)
    {
        _logger = logger;
        _aasCreatorService = aasCreatorService;
    }

    /// <summary>
    ///     Creates a new AAS for a given <paramref name="assetIdShort" /> with optional submodels.
    ///     If submodel parameters are provided in the request body, submodels will be generated and attached to the AAS.
    ///     If submodel generation fails, the entire operation fails and the AAS is not created.
    ///     Response contains id of the newly generated AAS Base64UrlEncoded and results from submodel generation if applicable.
    /// </summary>
    /// <param name="assetIdShort">The assetIdShort to be used for creating the AAS.</param>
    /// <param name="requestBody">Optional request body containing BlueprintsIds, Data, and Language for submodel generation.</param>
    /// <returns>
    ///     <see cref="CreateAasResponse"/>
    /// </returns>
    [HttpPost("{assetIdShort}")]
    [ProducesResponseType(typeof(CreateAasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CreateAasResponse>> CreateAas(
        [FromRoute] string assetIdShort,
        [FromBody] CreateAasRequest? requestBody = null)
    {
        _logger.LogTrace("Invoked AasCreator/{assetIdShort}/ with request: {request}", 
            assetIdShort, 
            requestBody != null ? $"BlueprintsIds: {string.Join(", ", requestBody.BlueprintsIds ?? Enumerable.Empty<string>())}" : "no request body");

        if (requestBody?.DefaultThumbnail != null && string.IsNullOrWhiteSpace(requestBody.DefaultThumbnail.Path))
        {
            return BadRequest("DefaultThumbnail.Path is required when DefaultThumbnail is provided.");
        }

        var aasCreationResult = await _aasCreatorService.CreateAasWithSubmodelsAsync(
            assetIdShort,
            requestBody?.BlueprintsIds,
            requestBody?.Data,
            requestBody?.Language,
            requestBody?.Debug ?? false,
            requestBody?.DefaultThumbnail);

        switch (aasCreationResult.status)
        {
            case AasCreationStatus.Created:
                var base64EncodedAssetId = Base64StringDeAndEncoder.EncodeTo64(aasCreationResult.aasIds.assetId);
                var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasCreationResult.aasIds.aasId);
                var createAasResponse = new CreateAasResponse
                {
                    AssetId = aasCreationResult.aasIds.assetId,
                    Base64EncodedAssetId = base64EncodedAssetId,
                    AasId = aasCreationResult.aasIds.aasId,
                    Base64EncodedAasId = base64EncodedAasId,
                    SubmodelResults = aasCreationResult.submodelResults,
                    AasRepoUrl = aasCreationResult.aasRepoUrl ?? string.Empty
                };

                return Ok(createAasResponse);
            case AasCreationStatus.AlreadyExists:
                _logger.LogTrace("Did not create AAS. AAS with id {aasId} already exists.",
                    aasCreationResult.aasIds.aasId);
                return BadRequest(
                    "There is already an AAS with the generated AasId. Please create a AasId yourself and put the AAS to the AasServer directly.");
            case AasCreationStatus.UnknownError:
            default:
                _logger.LogTrace("An error occured during AAS creation: {errorMessage}",
                    aasCreationResult.errorMessage);
                
                // If there are submodel results, include them in the error response
                if (aasCreationResult.submodelResults.Any())
                {
                    var errorResponse = new CreateAasResponse
                    {
                        AssetId = aasCreationResult.aasIds.assetId,
                        Base64EncodedAssetId = Base64StringDeAndEncoder.EncodeTo64(aasCreationResult.aasIds.assetId),
                        AasId = aasCreationResult.aasIds.aasId,
                        Base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasCreationResult.aasIds.aasId),
                        SubmodelResults = aasCreationResult.submodelResults
                    };
                    return BadRequest(errorResponse);
                }
                
                return StatusCode(StatusCodes.Status500InternalServerError, aasCreationResult.errorMessage);
        }
    }
}