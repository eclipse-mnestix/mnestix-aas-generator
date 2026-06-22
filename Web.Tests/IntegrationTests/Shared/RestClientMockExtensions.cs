using Moq;
using RestSharp;

namespace Web.Tests.IntegrationTests.Shared
{
    public static class RestClientMockExtensions
    {
        public static void ShouldHaveCalledGet(this Mock<IRestClient> mock, string endpoint, Times? times = null)
        {
            mock.Verify(x => x.ExecuteAsync(It.Is<RestRequest>(r =>
                    r.Resource == endpoint && r.Method == Method.Get),
                    It.IsAny<CancellationToken>()),
                    times ?? Times.Once()
                );
        }
    }
}
