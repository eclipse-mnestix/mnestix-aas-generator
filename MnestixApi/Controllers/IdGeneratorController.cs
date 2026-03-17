using MnestixCore.Dtos;
using MnestixCore.IdGenerator.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MnestixApi.Controllers;

/// <summary>
/// This controller provides endpoints to generate IDs used for AAS or submodels.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class IdGeneratorController : ControllerBase
{
    /// <summary>
    /// Generates a set of ids which is used to create a new AAS.
    /// Response contains:
    /// - AasId
    /// - AasIdShort
    /// - AssetId
    /// - AssetIdShort
    /// </summary>
    /// <param name="aasIdGeneratorService"></param>
    /// <param name="assetIdShort">The assetIdShort which must be used for generating ids.</param>
    /// <returns><see cref="AasIds"/></returns>
    [HttpGet("aasIds/{assetIdShort}")]
    [ProducesResponseType(typeof(AasIds), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GenerateIds([FromServices] IAasIdGeneratorService aasIdGeneratorService, [FromRoute] string assetIdShort)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShort);
        return Ok(aasIds);
    }

    /// <summary>
    /// Generates a set of ids which is used to create a new AAS.
    /// Response contains:
    /// - AasId
    /// - AasIdShort
    /// - AssetId
    /// - AssetIdShort
    /// </summary>
    /// <returns><see cref="AasIds"/></returns>
    [HttpGet("aasIds/")]
    [ProducesResponseType(typeof(AasIds), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GenerateIds([FromServices] IAasIdGeneratorService aasIdGeneratorService)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync();
        return Ok(aasIds);
    }

    /// <summary>
    /// Generates submodel ids as configured in MnestixIdGeneratorSettings.
    /// </summary>
    /// <param name="aasIdGeneratorService"></param>
    /// <param name="count">Amount of submodels id to be generated.</param>
    /// <returns>List of generated submodel ids</returns>
    [HttpGet("submodelIds/{count}")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult> GenerateSubmodelIds([FromServices] IAasIdGeneratorService aasIdGeneratorService, [FromRoute] uint count)
    {
        var submodelIds = await aasIdGeneratorService.GenerateSubmodelIdsAsync(count);
        return Ok(submodelIds);
    }
}