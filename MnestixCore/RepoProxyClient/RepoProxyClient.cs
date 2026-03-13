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
    public async Task<(bool IsSuccess, string Result)> GetAsync(string repoProxyPath)
    {
        var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
        var request = new RestRequest("/" + repoProxyPath);
        request.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);

        var response = await client.ExecuteAsync(request);

        if (response.IsSuccessful == false || response.Content == null)
        {
            return new ValueTuple<bool, string>(false, response.ErrorMessage ?? "Could not get from repository.");
        }

        return new ValueTuple<bool, string>(true, response.Content);
    }

    /// <inheritdoc />
    public async Task<string?> PostAsync(string relativeRepoProxyPath, string jsonContent)
    {
        var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
        var restRequest = new RestRequest("/" + relativeRepoProxyPath)
        {
            RequestFormat = DataFormat.Json,
            Method = Method.Post
        };

        restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
        restRequest.AddBody(JObject.Parse(jsonContent).ToString(), "application/json");

        var response = await client.PostAsync(restRequest);

        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPostShell,
                $"Could not post: {response.Content} Code: {response.StatusCode}",
                response.ErrorException);
        }

        return response.Content;
    }

    /// <inheritdoc />
    public async Task<string?> PutAsync(string relativeRepoProxyPath, string jsonContent)
    {
        var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
        var restRequest = new RestRequest("/" + relativeRepoProxyPath)
        {
            RequestFormat = DataFormat.Json,
            Method = Method.Put
        };

        restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
        restRequest.AddBody(JObject.Parse(jsonContent).ToString(), "application/json");

        var response = await client.PutAsync(restRequest);

        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created
            && response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPostShell,
                $"Could not put: {response.Content} " +
                $"Code: {response.StatusCode}",
                response.ErrorException);
        }

        return response.Content;
    }

    /// <inheritdoc />
    public async Task<string?> PostSubmodelWithReferenceAsync(string aasIdBase64, string submodelIdNotEncoded,
        string jsonContent)
    {
        var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
        var restRequest = new RestRequest(_repoProxyOptions.SubmodelPath)
        {
            RequestFormat = DataFormat.Json,
            Method = Method.Post
        };

        restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
        restRequest.AddBody(JObject.Parse(jsonContent).ToString(), "application/json");

        var response = await client.PostAsync(restRequest);

        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
        {
            throw new RepoProxyException(
                ErrorCodes.CouldNotPutSubmodel,
                $"Could not post submodel: {response.Content} " +
                $"Code: {response.StatusCode}",
                response.ErrorException);
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

        response = await client.PostAsync(restRequest);

        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
        {
            throw new RepoProxyException(ErrorCodes.CouldNotPutSubmodel,
                $"Could not post submodel-reference: {response.Content} " +
                $"Code: {response.StatusCode}",
                response.ErrorException);
        }

        return response.Content;
    }

    /// <inheritdoc />
    public async Task<string?> PutFileContent(string repoProxyPath, string fileName, byte[] fileContent)
    {
        var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());

        var restRequest = new RestRequest(repoProxyPath)
        {
            Method = Method.Put
        };

        restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);
        restRequest.AddFile("file", fileContent, fileName);

        var response = await client.PutAsync(restRequest);

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

    public async Task<string?> PatchAsync(string relativeRepoProxyPath, string value)
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

        var response = await client.PatchAsync(restRequest);

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

    public async Task<bool> DeleteAsync(string relativeRepoProxyPath)
    {
        var client = await httpClientProvider.GetConfiguredClientAsync(baseUrlProvider.GetBaseUrl());
        var restRequest = new RestRequest("/" + relativeRepoProxyPath)
        {
            RequestFormat = DataFormat.Json,
            Method = Method.Delete
        };

        restRequest.AddHeader(ApiKeyHeaderKey, _customerEndpointsSecurityOptions.ApiKey);

        var response = await client.DeleteAsync(restRequest);

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

    public string GetAasRepositoryUrl()
    {
        return baseUrlProvider.GetBaseUrl();
    }
}