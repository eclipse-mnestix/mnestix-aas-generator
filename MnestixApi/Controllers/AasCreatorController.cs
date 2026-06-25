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
    /// <param name="overwrite">When true, overwrite an existing AAS shell with the generated id instead of returning 409.</param>
    /// <param name="requestBody">Optional request body containing BlueprintsIds, Data, and Language for submodel generation.</param>
    /// <returns>
    ///     <see cref="CreateAasResponse"/>
    /// </returns>
    [HttpPost("{assetIdShort}")]
    [ProducesResponseType(typeof(CreateAasResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateAasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(CreateAasConflictResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CreateAasResponse>> CreateAas(
        [FromRoute] string assetIdShort,
        [FromQuery] bool overwrite = false,
        [FromBody] CreateAasRequest? requestBody = null)
    {
        _logger.LogTrace("Invoked AasCreator/{assetIdShort}/ with overwrite={overwrite} and request: {request}",
            assetIdShort,
            overwrite,
            requestBody != null ? $"BlueprintsIds: {string.Join(", ", requestBody.BlueprintsIds ?? Enumerable.Empty<string>())}" : "no request body");

        if (requestBody?.DefaultThumbnail != null && string.IsNullOrWhiteSpace(requestBody.DefaultThumbnail.Path))
        {
            return BadRequest("DefaultThumbnail.Path is required when DefaultThumbnail is provided.");
        }

        var options = new AasCreationOptions
        {
            AssetKind = requestBody?.AssetKind ?? AssetKind.Instance,
            Extensions = requestBody?.Extensions,
            SpecificAssetIds = requestBody?.SpecificAssetIds,
            Administration = requestBody?.Administration,
            DefaultThumbnail = requestBody?.DefaultThumbnail,
            DerivedFrom = requestBody?.DerivedFrom
        };

        var aasCreationResult = await _aasCreatorService.CreateAasWithSubmodelsAsync(
            assetIdShort,
            requestBody?.BlueprintsIds,
            requestBody?.Data,
            requestBody?.Language,
            requestBody?.Debug ?? false,
            requestBody?.GlobalAssetId,
            overwrite,
            options,
            requestBody?.SubmodelIds);

        switch (aasCreationResult.status)
        {
            case AasCreationStatus.Created:
                return StatusCode(StatusCodes.Status201Created, BuildResponse(aasCreationResult));
            case AasCreationStatus.Overwritten:
                return Ok(BuildResponse(aasCreationResult));
            case AasCreationStatus.Conflict:
                _logger.LogTrace("AAS with id {aasId} already exists and overwrite=false.",
                    aasCreationResult.aasIds.aasId);
                return Conflict(new CreateAasConflictResponse
                {
                    Error = aasCreationResult.errorMessage ?? "AAS already exists, use overwrite=true to replace",
                    OrphanedSubmodelIds = aasCreationResult.orphanedSubmodelIds ?? Enumerable.Empty<string>()
                });
            case AasCreationStatus.AlreadyExists:
                _logger.LogTrace("Did not create AAS. AAS with id {aasId} already exists.",
                    aasCreationResult.aasIds.aasId);
                return BadRequest(
                    "There is already an AAS with the generated AasId. Please create a AasId yourself and put the AAS to the AasServer directly.");
            case AasCreationStatus.GenerationFailed:
                _logger.LogTrace("Submodel generation or input validation failed during AAS creation: {errorMessage}",
                    aasCreationResult.errorMessage);

                if (aasCreationResult.submodelResults.Any())
                {
                    return BadRequest(BuildResponse(aasCreationResult));
                }

                return BadRequest(aasCreationResult.errorMessage);
            case AasCreationStatus.UnknownError:
                _logger.LogTrace("An error occurred during AAS creation: {errorMessage}",
                    aasCreationResult.errorMessage);

                return StatusCode(StatusCodes.Status500InternalServerError, aasCreationResult.errorMessage);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(aasCreationResult.status),
                    aasCreationResult.status,
                    "Unhandled AAS creation status.");
        }
    }

    private static CreateAasResponse BuildResponse(AasCreationWithSubmodelsResult result)
    {
        return new CreateAasResponse
        {
            AssetId = result.aasIds.assetId,
            Base64EncodedAssetId = Base64StringDeAndEncoder.EncodeTo64(result.aasIds.assetId),
            AasId = result.aasIds.aasId,
            Base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(result.aasIds.aasId),
            SubmodelResults = result.submodelResults,
            AasRepoUrl = result.aasRepoUrl ?? string.Empty,
            PreviousAas = result.previousAas
        };
    }
}