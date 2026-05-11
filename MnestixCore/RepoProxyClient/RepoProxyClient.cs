using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
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

    // Canonical XSD value-type mapping (BaSyx Go requires lowercase)
    private static readonly Dictionary<string, string> ValueTypeCaseMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xs:string"] = "xs:string",
        ["xs:boolean"] = "xs:boolean",
        ["xs:integer"] = "xs:integer",
        ["xs:int"] = "xs:int",
        ["xs:long"] = "xs:long",
        ["xs:short"] = "xs:short",
        ["xs:decimal"] = "xs:decimal",
        ["xs:double"] = "xs:double",
        ["xs:float"] = "xs:float",
        ["xs:dateTime"] = "xs:dateTime",
        ["xs:date"] = "xs:date",
        ["xs:time"] = "xs:time",
        ["xs:anyURI"] = "xs:anyURI",
        ["xs:base64Binary"] = "xs:base64Binary",
        ["xs:hexBinary"] = "xs:hexBinary",
        ["xs:byte"] = "xs:byte",
        ["xs:unsignedByte"] = "xs:unsignedByte",
        ["xs:unsignedShort"] = "xs:unsignedShort",
        ["xs:unsignedInt"] = "xs:unsignedInt",
        ["xs:unsignedLong"] = "xs:unsignedLong",
        ["xs:positiveInteger"] = "xs:positiveInteger",
        ["xs:nonNegativeInteger"] = "xs:nonNegativeInteger",
        ["xs:negativeInteger"] = "xs:negativeInteger",
        ["xs:nonPositiveInteger"] = "xs:nonPositiveInteger",
        ["xs:duration"] = "xs:duration",
        ["xs:gDay"] = "xs:gDay",
        ["xs:gMonth"] = "xs:gMonth",
        ["xs:gMonthDay"] = "xs:gMonthDay",
        ["xs:gYear"] = "xs:gYear",
        ["xs:gYearMonth"] = "xs:gYearMonth",
    };

    /// <summary>
    /// Normalizes a JSON payload for compatibility with BaSyx Go's stricter AAS v3 compliance.
    /// Applies 7 rules: strip nulls, remove dataSpecification, strip kind from non-Submodel,
    /// strip parent, normalize valueType, inject qualifier valueType, coerce Property.value to string.
    /// </summary>
    internal static JObject NormalizeJsonForRepository(JObject json)
    {
        NormalizeToken(json, isRoot: true);
        return json;
    }

    private static void NormalizeToken(JToken token, bool isRoot = false)
    {
        switch (token)
        {
            case JObject obj:
                // Collect properties to remove (avoid modifying during enumeration)
                var propsToRemove = new List<string>();

                foreach (var prop in obj.Properties().ToList())
                {
                    // Rule 1: Strip null-valued properties
                    if (prop.Value.Type == JTokenType.Null)
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 2: Remove deprecated dataSpecification property
                    if (prop.Name is "dataSpecification" or "hasDataSpecification")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 4: Strip parent back-references
                    if (prop.Name == "parent")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Strip v2 fields from Key objects
                    if (prop.Name is "local" or "idType" or "index")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Strip v2 ordered / allowDuplicates from SubmodelElementCollections
                    if (prop.Name is "ordered" or "allowDuplicates")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }
                }

                foreach (var name in propsToRemove)
                {
                    obj.Remove(name);
                }

                // Rule 3: Strip kind from non-Submodel elements
                // Only the root-level Submodel (which has modelType=Submodel) may keep "kind"
                var modelType = obj["modelType"]?.Value<string>();
                if (obj.ContainsKey("kind") && modelType != null && modelType != "Submodel")
                {
                    obj.Remove("kind");
                }

                // Rule 5: Normalize valueType to canonical XSD case
                if (obj["valueType"] is JToken vt && vt.Type == JTokenType.String)
                {
                    var raw = vt.Value<string>();
                    if (raw != null && ValueTypeCaseMap.TryGetValue(raw, out var canonical))
                    {
                        obj["valueType"] = canonical;
                    }
                }

                // Rule 6: Inject valueType on qualifiers missing it
                if (modelType == null && obj["type"] != null && obj["value"] != null && !obj.ContainsKey("valueType"))
                {
                    // This looks like a qualifier object (has "type" and "value" but no "modelType")
                    var parentArray = obj.Parent as JArray;
                    var parentProp = parentArray?.Parent as JProperty;
                    if (parentProp?.Name == "qualifiers")
                    {
                        obj["valueType"] = "xs:string";
                    }
                }

                // Rule 7: Coerce non-string Property.value to string
                if (modelType == "Property" && obj["value"] is JToken val)
                {
                    if (val.Type is JTokenType.Integer or JTokenType.Float or JTokenType.Boolean)
                    {
                        obj["value"] = val.ToString();
                    }
                }

                // Recurse into remaining properties
                foreach (var prop in obj.Properties().ToList())
                {
                    NormalizeToken(prop.Value);
                }
                break;

            case JArray arr:
                foreach (var item in arr.ToList())
                {
                    NormalizeToken(item);
                }
                break;
        }
    }

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
            var normalized = NormalizeJsonForRepository(JObject.Parse(jsonContent));
            restRequest.AddBody(normalized.ToString(), "application/json");

            var response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new RepoProxyException(
                    ErrorCodes.CouldNotPostShell,
                    $"Could not post: {response.Content} Code: {response.StatusCode}",
                    response.ErrorException);
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
            var normalized = NormalizeJsonForRepository(JObject.Parse(jsonContent));
            restRequest.AddBody(normalized.ToString(), "application/json");

            var response = await client.ExecuteAsync(restRequest);

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
            var normalized = NormalizeJsonForRepository(JObject.Parse(jsonContent));
            restRequest.AddBody(normalized.ToString(), "application/json");

            var response = await client.ExecuteAsync(restRequest);

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

            response = await client.ExecuteAsync(restRequest);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Created)
            {
                throw new RepoProxyException(ErrorCodes.CouldNotPutSubmodel,
                    $"Could not post submodel-reference: {response.Content} " +
                    $"Code: {response.StatusCode}",
                    response.ErrorException);
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