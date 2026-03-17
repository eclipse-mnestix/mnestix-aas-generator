namespace MnestixCore.Dtos.AppSettingsOptions;

/// <summary>
/// Holds the configuration of the Mongo database used by the BaSyx AasServer and in Mnestix included lookup service.
/// </summary>
public class BasyxDbConnectionConfiguration
{
    /// <summary>
    /// Connection string for the MongoDb
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