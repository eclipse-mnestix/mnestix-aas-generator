using FluentAssertions;
using MnestixCore.RestClientProvider;
using RestSharp;

namespace Core.Tests.RepoProxyClient;

[TestFixture]
public class HttpClientProviderTests
{
    private const string BaseUrl = "https://localhost/";

    [Test]
    public async Task GetConfiguredClientAsync_CalledConcurrently_ReturnsOneSharedInstance()
    {
        var sut = new HttpClientProvider();

        var callers = Enumerable.Range(0, 50)
            .Select(_ => sut.GetConfiguredClientAsync(BaseUrl))
            .ToList();
        var clients = await Task.WhenAll(callers);

        clients.Should().OnlyContain(c => ReferenceEquals(c, clients[0]),
            "concurrent callers must not each build their own RestClient");
    }

    [Test]
    public async Task GetConfiguredClientAsync_CalledTwiceSequentially_ReturnsSameInstance()
    {
        var sut = new HttpClientProvider();

        var first = await sut.GetConfiguredClientAsync(BaseUrl);
        var second = await sut.GetConfiguredClientAsync(BaseUrl);

        second.Should().BeSameAs(first);
    }
}
