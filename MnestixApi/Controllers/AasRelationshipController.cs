using MnestixCore.AasInheritance;
using MnestixCore.AasInheritance.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MnestixApi.Controllers;

/// <summary>
/// This controller provides endpoints to navigate along relationships between AAS.
/// </summary>
[ApiVersion("1.0", Deprecated = true)]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},ApiKey")]
public class AasRelationshipController : ControllerBase
{
    private readonly IAasInheritanceService _aasInheritanceService;

    /// <inheritdoc />
    public AasRelationshipController(IAasInheritanceService aasInheritanceService)
    {
        _aasInheritanceService = aasInheritanceService;
    }

    /// <summary>
    /// Returns all asset administration shells that have a direct derivedFrom dependency on the given asset administration shell
    /// </summary>
    /// <param name="aasId">The Id of the AAS to search inheritors for</param>
    /// <returns>A list of (AasId, AssetIdShort)-tuples that derive from the given AasId</returns>
    [HttpGet("GetDerivedFrom")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<Aas>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDerivedFrom([FromQuery] string aasId)
    {
        if (string.IsNullOrWhiteSpace(aasId))
        {
            return BadRequest();
        }

        var result = await _aasInheritanceService.GetDerivedFrom(aasId);
        return Ok(result);
    }
}