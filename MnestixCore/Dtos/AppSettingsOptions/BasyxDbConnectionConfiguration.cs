namespace MnestixCore.Dtos.AppSettingsOptions;

/// <summary>
/// Holds the configuration of the database used by the BaSyx server and in Mnestix included lookup service.
/// </summary>
public class BasyxDbConnectionConfiguration
{
    /// <summary>
    /// Connection string for PostgreSQL (used by BaSyx Go).
    /// </summary>
    public string PostgresConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Name of the PostgreSQL table where AAS documents are stored.
    /// Defaults to "aas" which is the BaSyx Go convention.
    /// </summary>
    public string AasTableName { get; init; } = "aas";

    /// <summary>
    /// Connection string for the MongoDb (legacy, used by BaSyx Java).
    /// </summary>
    public string MongoConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Name of the database which is also configured in an ENV variable of AasServer.
    /// </summary>
    public string DatabaseName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the collection in the database which holds the AAS.
    /// This is also configured in an ENV variable of AasServer. 
    /// </summary>
    public string AasCollectionName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the collection in the database <see cref="DatabaseName"/> where the lookup service stores the
    /// references from AssetId to AasIds.
    /// </summary>
    public string LookupServiceCollectionName { get; init; } = string.Empty;
}