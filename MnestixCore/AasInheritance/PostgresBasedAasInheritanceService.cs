using Microsoft.Extensions.Options;
using MnestixCore.AasInheritance.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace MnestixCore.AasInheritance;

public class PostgresBasedAasInheritanceService : IAasInheritanceService
{
    private readonly BasyxDbConnectionConfiguration _config;

    public PostgresBasedAasInheritanceService(IOptions<BasyxDbConnectionConfiguration> config)
    {
        _config = config.Value;
    }

    /// <inheritdoc />
    public async Task<List<Aas>> GetDerivedFrom(string aasId)
    {
        var decodedAasId = Uri.UnescapeDataString(aasId);

        await using var conn = new NpgsqlConnection(_config.PostgresConnectionString);
        await conn.OpenAsync();

        // BaSyx Go stores AAS as JSONB in a table named after the collection.
        // Query for AAS where derivedFrom.keys[0].value matches the given aasId.
        var sql = $"""
            SELECT aas_id, aas_data
            FROM {_config.AasTableName}
            WHERE aas_data->'derivedFrom'->'keys'->0->>'value' = @aasId
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@aasId", decodedAasId);

        var result = new List<Aas>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var aasIdValue = reader.GetString(0);
            var aasDataJson = reader.GetString(1);

            var aasData = JObject.Parse(aasDataJson);
            var assetIdShort = aasData.SelectToken(
                "assetInformation.specificAssetIds[?(@.name=='assetIdShort')].value")?.Value<string>() ?? string.Empty;

            result.Add(new Aas(aasIdValue, assetIdShort));
        }

        return result;
    }
}
