using System.Net;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.RestClientProvider.Interfaces;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace MnestixCore.RepoProxyClient;

public class RepoProxyClient(
        IOptions<RepoProxyOptions> repoProxyOptions,
        IOptions<CustomerEndpointsSecurityOptions> customerEndpointsSecurityOptions,
        BaseUrlProvider baseUrlProvider,
        IHttpClientProvider httpClientProvider
    ) : IRepoProxyClient
{
    private const string ApiKeyHeaderKey = "X-API-KEY";
    private readonly RepoProxyOptions _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));

    private readonly CustomerEndpointsSecurityOptions _customerEndpointsSecurityOptions = customerEndpointsSecurityOptions.Value ??
                                                                                          throw new ArgumentNullException(nameof(customerEndpointsSecurityOptions));

    /// <inheritdoc />
    public async Task<string?> GetAsync(string repoProxyPath)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
            var request = new RestRequest("/" + repoProxyPath);
            request.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {response.Content}",
                    null,
                    response.StatusCode);
            }

            return response.Content;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotGet,
                $"Could not get from repository: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<string?> PostAsync(string relativeRepoProxyPath, string jsonContent)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
            var restRequest = new RestRequest("/" + relativeRepoProxyPath)
            {
                RequestFormat = DataFormat.Json,
                Method = Method.Post
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
            var normalized = AasJsonNormalizer.NormalizeJsonForRepository(JObject.Parse(jsonContent));
            restRequest.AddBody(normalized.ToString(), "application/json");

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotPostShell,
                    $"Could not post: {response.Content} Code: {response.StatusCode}",
                    response.StatusCode,
                    response.Content);
            }

            return response.Content;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPostShell,
                $"Could not post: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<string?> PutAsync(string relativeRepoProxyPath, string jsonContent)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
            var restRequest = new RestRequest("/" + relativeRepoProxyPath)
            {
                RequestFormat = DataFormat.Json,
                Method = Method.Put
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
            var normalized = AasJsonNormalizer.NormalizeJsonForRepository(JObject.Parse(jsonContent));
            restRequest.AddBody(normalized.ToString(), "application/json");

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created
                && response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotPostShell,
                    $"Could not put: {response.Content} Code: {response.StatusCode}",
                    response.StatusCode,
                    response.Content);
            }

            return response.Content;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPostShell,
                $"Could not put: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<string?> PostSubmodelWithReferenceAsync(string aasIdBase64, string submodelIdNotEncoded,
        string jsonContent)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
            var restRequest = new RestRequest(_repoProxyOptions.SubmodelPath)
            {
                RequestFormat = DataFormat.Json,
                Method = Method.Post
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
            var normalized = AasJsonNormalizer.NormalizeJsonForRepository(JObject.Parse(jsonContent));
            restRequest.AddBody(normalized.ToString(), "application/json");

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotPutSubmodel,
                    $"Could not post submodel: {response.Content} Code: {response.StatusCode}",
                    response.StatusCode,
                    response.Content);
            }

            var submodelReference =
                "{\"type\": \"ModelReference\",\"keys\": [{\"type\": \"Submodel\",\"value\": \"" +
                submodelIdNotEncoded + "\"}]}";

            restRequest = new RestRequest($"{_repoProxyOptions.AasPath}/{aasIdBase64}/submodel-refs")
            {
                RequestFormat = DataFormat.Json,
                Method = Method.Post
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
            restRequest.AddBody(JObject.Parse(submodelReference).ToString(), "application/json");

            response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new RepoProxyException(ErrorCodes.CouldNotPutSubmodel,
                    $"Could not post submodel-reference: {response.Content} Code: {response.StatusCode}",
                    response.StatusCode,
                    response.Content);
            }

            return response.Content;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPutSubmodel,
                $"Could not post submodel: {ex.Message}",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<string?> PutFileContent(string repoProxyPath, string fileName, byte[] fileContent)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());

            var restRequest = new RestRequest(repoProxyPath)
            {
                Method = Method.Put
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
            restRequest.AddFile("file", fileContent, fileName);

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created
                && response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotPostShell,
                    $"Could not put file: {response.Content} " +
                    $"Code: {response.StatusCode}",
                    response.ErrorException);
            }

            return response.Content;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPostShell,
                $"Could not put file: {ex.Message}",
                ex);
        }
    }

    public async Task<string?> PatchAsync(string relativeRepoProxyPath, string value)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
            var restRequest = new RestRequest("/" + relativeRepoProxyPath)
            {
                RequestFormat = DataFormat.Json,
                Method = Method.Patch
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
            var token = JToken.FromObject(value);
            restRequest.AddBody(token.ToString(Newtonsoft.Json.Formatting.None), "application/json");

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created
                && response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotPatchSubmodel,
                    $"Code: {response.StatusCode}",
                    response.ErrorException);
            }

            return response.Content;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPatchSubmodel,
                $"Could not patch: {ex.Message}",
                ex);
        }
    }

    public async Task<bool> DeleteAsync(string relativeRepoProxyPath)
    {
        try
        {
            var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
            var restRequest = new RestRequest("/" + relativeRepoProxyPath)
            {
                RequestFormat = DataFormat.Json,
                Method = Method.Delete
            };

            restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new RepoProxyException(
                   ErrorCodes.CouldNotFind,
                   $"Code: {response.StatusCode}",
                   response.ErrorException);
            }

            if (response.StatusCode < HttpStatusCode.OK || response.StatusCode > HttpStatusCode.IMUsed)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotDelete,
                    $"Code: {response.StatusCode}",
                    response.ErrorException);
            }

            return true;
        }
        catch (Exception ex) when (ex is not RepoProxyException)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotDelete,
                $"Could not delete: {ex.Message}",
                ex);
        }
    }

    public string GetAasRepositoryUrl()
    {
        return baseUrlProvider.GetBaseUrl();
    }
}