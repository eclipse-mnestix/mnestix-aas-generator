using FluentAssertions;
using MnestixCore.RestClientProvider.Interfaces;
using MnestixCore.RestClientProvider.OpenIdClientProvider;
using Moq;
using RestSharp;

namespace Core.Tests.RepoProxyClient;

[TestFixture]
public class HttpClientTokenProviderTests
{
    private const string BaseUrl = "https://localhost/";

    [Test]
    public async Task GetConfiguredClientAsync_CalledConcurrently_RequestsTokenExactlyOnce()
    {
        // ARRANGE: gate the token service so every concurrent caller is in flight
        // before the first token request completes. A per-call "check null then fetch"
        // would let all of them observe a null client and each fetch a token.
        var release = new TaskCompletionSource();
        var callCount = 0;
        var accessTokenServiceMock = new Mock<IAccessTokenService>();
        accessTokenServiceMock
            .Setup(s => s.GetTokenAsync())
            .Returns(async () =>
            {
                Interlocked.Increment(ref callCount);
                await release.Task;
                return "token";
            });

        var sut = new HttpClientTokenProvider(accessTokenServiceMock.Object);

        // ACT: launch many callers, then release the gate
        var callers = Enumerable.Range(0, 50)
            .Select(_ => sut.GetConfiguredClientAsync(BaseUrl))
            .ToList();
        release.SetResult();
        var clients = await Task.WhenAll(callers);

        // ASSERT
        accessTokenServiceMock.Verify(s => s.GetTokenAsync(), Times.Once);
        clients.Should().OnlyContain(c => ReferenceEquals(c, clients[0]),
            "every caller must receive the one shared client");
    }

    [Test]
    public async Task GetConfiguredClientAsync_CalledTwiceSequentially_ReturnsSameInstance()
    {
        var accessTokenServiceMock = new Mock<IAccessTokenService>();
        accessTokenServiceMock.Setup(s => s.GetTokenAsync()).ReturnsAsync("token");
        var sut = new HttpClientTokenProvider(accessTokenServiceMock.Object);

        var first = await sut.GetConfiguredClientAsync(BaseUrl);
        var second = await sut.GetConfiguredClientAsync(BaseUrl);

        second.Should().BeSameAs(first);
        accessTokenServiceMock.Verify(s => s.GetTokenAsync(), Times.Once);
    }
}
