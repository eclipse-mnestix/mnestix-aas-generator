namespace MnestixCore.RepoProxyClient.Interfaces;

public interface IRepoProxyClient
{
    /// <summary>
    /// Gets a submodel from the given path via the repository proxy.
    /// </summary>
    /// <param name="repoProxyPath">The relative path to the submodel.</param>
    /// <returns>Response content from repository</returns>
    Task<string?> GetAsync(string repoProxyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the given <paramref name="jsonContent" /> to the given path via POST-Call to the repository proxy. 
    /// </summary>
    /// <param name="relativeRepoProxyPath">The relative path to the repo</param>
    /// <param name="jsonContent">The content to post as json</param>
    /// <returns>Response content from repository</returns>
    Task<string?> PostAsync(string relativeRepoProxyPath, string jsonContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the given <paramref name="jsonContent" /> to the given path via PUT-Call to the repository proxy. 
    /// </summary>
    /// <param name="relativeRepoProxyPath">The relative path to the repo</param>
    /// <param name="jsonContent">The content to put as json</param>
    /// <returns>Response content from repository</returns>
    Task<string?> PutAsync(string relativeRepoProxyPath, string jsonContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts the given <paramref name="jsonContent" /> to the given AAS as submodel via POST-Call to the repository proxy. 
    /// </summary>
    /// <param name="aasIdBase64">The id of the AAS the submodel belongs to</param>
    /// <param name="submodelIdNotEncoded">The not encoded id of the submodel</param>
    /// <param name="jsonContent">The content to put as json</param>
    /// <returns>Response content from repository</returns>
    Task<string?> PostSubmodelWithReferenceAsync(string aasIdBase64, string submodelIdNotEncoded, string jsonContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the content of an existing file element in an AAS or submodel element via a PUT call to the repository proxy. 
    /// </summary>
    /// <param name="repoProxyPath">The repository proxy path identifying the target file element</param>
    /// <param name="fileName">The name of the file being updated</param>
    /// <param name="fileContent">The binary content of the file to be updated</param>
    /// <returns>The response content from the repository as a string</returns>
    Task<string?> PutFileContent(string repoProxyPath, string fileName, byte[] fileContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the given <paramref name="value" /> to the given path via Patch-Call to the repository proxy. 
    /// </summary>
    /// <param name="relativeRepoProxyPath">The relative path to the repo</param>
    /// <param name="value">The value to be updated</param>
    /// <returns>Response content from repository</returns>
    Task<string?> PatchAsync(string relativeRepoProxyPath, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the given resource from the repository. 
    /// </summary>
    /// <param name="relativeRepoProxyPath">The relative path to the repo</param>
    /// <returns>True if successfully deleted, else False.</returns>
    Task<bool> DeleteAsync(string relativeRepoProxyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full URL of the AAS repository.
    /// </summary>
    /// <returns>The complete AAS repository URL including base URL and path.</returns>
    string GetAasRepositoryUrl();
}