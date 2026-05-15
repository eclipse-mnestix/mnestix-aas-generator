using Microsoft.Extensions.Options;
using MnestixCore.AasInheritance.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MnestixCore.AasInheritance;

public class MongoDbBasedAasInheritanceService : IAasInheritanceService
{
    private readonly MongoClient _mongoClient;
    private readonly BasyxDbConnectionConfiguration _basyxDbConnectionConfiguration;

    public MongoDbBasedAasInheritanceService(IOptions<BasyxDbConnectionConfiguration> basyxDbConnectionConfiguration)
    {
        _basyxDbConnectionConfiguration = basyxDbConnectionConfiguration.Value;
        _mongoClient = new MongoClient(_basyxDbConnectionConfiguration.MongoConnectionString);
    }

    /// <inheritdoc />
    public async Task<List<Aas>> GetDerivedFrom(string aasId)
    {
        var collection = _mongoClient
            .GetDatabase(_basyxDbConnectionConfiguration.DatabaseName)
            .GetCollection<BsonDocument>(_basyxDbConnectionConfiguration.AasCollectionName);

        var filter = Builders<BsonDocument>.Filter.Eq("derivedFrom.keys.0.value", Uri.UnescapeDataString(aasId));
        var foundElements = await collection.FindAsync(filter);
        var aasList = foundElements.ToList()
            .Select(res =>
            {
                var newAasId = res.GetElement("_id").Value.ToString() ?? string.Empty;
                var newAssetId = res.GetElement("assetInformation").Value.AsBsonDocument
                    .GetElement("specificAssetIds").Value.AsBsonArray
                    .FirstOrDefault(id => id["name"] == "assetIdShort")?.AsBsonDocument.GetElement("value").Value
                    .ToString() ?? string.Empty;
                return new Aas(newAasId, newAssetId);
            })
            .ToList();

        return aasList;
    }
}