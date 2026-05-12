using Microsoft.Extensions.Options;
using MnestixCore.AasInheritance.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Text.RegularExpressions;

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
        // Ensure the table name is a safe identifier (prevent SQL injection) and
        // use a parameter for the searched value.
        var sanitizedTable = SanitizeIdentifier(_config.AasTableName);
        var sql = $"SELECT aas_id, aas_data FROM {sanitizedTable} WHERE aas_data->'derivedFrom'->'keys'->0->>'value' = @aasId";

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

    private static string SanitizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Table name must not be null or empty.", nameof(identifier));

        // Allow only typical SQL identifier characters: letters, digits and underscores,
        // and must not start with a digit. This blocks malicious inputs like
        // "users; DROP TABLE ...". Adjust the regex if your naming rules differ.
        if (!Regex.IsMatch(identifier, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new ArgumentException("Invalid table name.", nameof(identifier));

        // Quote the identifier to be safe against reserved words. Double any internal
        // quotes as required by SQL identifier quoting rules.
        var quoted = identifier.Replace("\"", "\"\"");
        return '"' + quoted + '"';
    }
}
