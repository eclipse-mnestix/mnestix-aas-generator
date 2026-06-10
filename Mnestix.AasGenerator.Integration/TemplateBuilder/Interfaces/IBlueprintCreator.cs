namespace MnestixCore.TemplateBuilder.Interfaces;

public interface IBlueprintCreator
{
    /// <summary>
    /// Creates a new submodel (kind: instance) and PUTs it into the blueprint AAS.
    /// </summary>
    /// <param name="submodel">The default submodel from which a blueprint must be created.</param>
    /// <returns>Submodel identifier</returns>
    Task<string> CreateNewSubmodelInBlueprintAasAsync(string submodel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a submodel.
    /// The SubmodelIdShort of the submodel content is used to identify the submodel in the blueprint AAS to update.
    /// </summary>
    /// <param name="submodel">The submodel as json</param>
    /// <param name="submodelId">The id of the submodel</param>
    Task UpdateSubmodelInBlueprintAasAsync(string submodel, string submodelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a submodel.
    /// The SubmodelIdShort of the submodel content is used to identify the submodel in the blueprint AAS to delete.
    /// </summary>
    /// <param name="submodelId">The id of the submodel base64 encoded</param>
    Task DeleteSubmodelInBlueprintAasAsync(string submodelIdBase64Encoded, CancellationToken cancellationToken = default);
}