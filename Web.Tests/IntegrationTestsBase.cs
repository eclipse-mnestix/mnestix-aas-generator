using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MnestixApi;
using MnestixCore.RestClientProvider.Interfaces;
using Moq;
using System.Diagnostics;

namespace Web.Tests;

public class IntegrationTestsBase
{
    protected HttpClient? Client;
    protected Mock<IHttpClientProvider> HttpClientMock;
    protected IConfiguration _configuration;

    [SetUp]
    public void Setup()
    {
        HttpClientMock = new Mock<IHttpClientProvider>();

        var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddJsonFile("appsettings.json");
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Configuration:SubmodelTemplatesApiUrl"] = string.Empty,
                        ["Configuration:SubmodelBlueprintsApiUrl"] = string.Empty,
                        ["Features:RequiredShells"] = "false"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHttpClientProvider));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddSingleton(HttpClientMock.Object);
                });
            });

        Client = application.CreateClient();
        Client.DefaultRequestHeaders.Add("X-API-KEY", "verySecureApiKey");

        _configuration = application.Services.GetRequiredService<IConfiguration>();
    }
    
    protected async Task<string> GetResponseContentAndEnsureStatusCodeAsync(string requestUri, int? statusCode = null)
    {
        Debug.Assert(Client != null, nameof(Client) + " != null");

        var response = await Client.GetAsync(requestUri);
        var contentString = await response.Content.ReadAsStringAsync();
        LogResponseAndEnsureStatusCode(contentString, response, statusCode);
        return contentString;
    }
    
    protected async Task<string> PostContentAndEnsureSuccessStatusCodeAsync(string requestUri, HttpContent? content, int? statusCode = null)
    {
        Debug.Assert(Client != null, nameof(Client) + " != null");

        var response = await Client.PostAsync(requestUri, content);
        var contentString = await response.Content.ReadAsStringAsync();
        LogResponseAndEnsureStatusCode(contentString, response, statusCode);
        return contentString;
    }
    
    protected async Task PatchContentAndEnsureSuccessStatusCodeAsync(string requestUri, HttpContent? content, int? statusCode = null)
    {
        Debug.Assert(Client != null, nameof(Client) + " != null");
        
        var response = await Client.PatchAsync(requestUri, content);
        var contentString = await response.Content.ReadAsStringAsync();
        LogResponseAndEnsureStatusCode(contentString, response, statusCode);
    }

    protected async Task PutContentAndEnsureSuccessStatusCodeAsync(string requestUri, HttpContent? content = null, int? statusCode = null)
    {
        Debug.Assert(Client != null, nameof(Client) + " != null");

        var response = await Client.PutAsync(requestUri, content);
        var contentString = await response.Content.ReadAsStringAsync();
        LogResponseAndEnsureStatusCode(contentString, response, statusCode);
    }

    protected async Task DeleteAndEnsureSuccessStatusCodeAsync(string requestUri, int? statusCode = null)
    {
        Debug.Assert(Client != null, nameof(Client) + " != null");

        var response = await Client.DeleteAsync(requestUri);
        var contentString = await response.Content.ReadAsStringAsync();
        LogResponseAndEnsureStatusCode(contentString, response, statusCode);
    }

    private static void LogResponseAndEnsureStatusCode(string contentString, HttpResponseMessage response, int? statusCode = null)
    {
        if (statusCode == null)
        {
            response.EnsureSuccessStatusCode();
        }
        else
        {
            ((int)response.StatusCode).Should().Be(statusCode!);
        }
    }

    [TearDown]
    public void TearDown()
    {
        // Dispose of the client to release resources
        if (Client != null)
        {
            Client.Dispose();
            Client = null; // Set to null to prevent accidental usage
        }
    }
}