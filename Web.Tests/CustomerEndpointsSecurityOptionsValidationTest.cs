using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixApi.ApiKeyAuthorization;
using MnestixCore.Dtos.AppSettingsOptions;
using Moq;

namespace Web.Tests;

public class CustomerEndpointsSecurityOptionsValidationTest
{
    private Mock<ILogger<CustomerEndpointsSecurityOptionsValidation>> _loggerMock = null!;
    private CustomerEndpointsSecurityOptionsValidation _cut = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<CustomerEndpointsSecurityOptionsValidation>>();
        _cut = new CustomerEndpointsSecurityOptionsValidation(_loggerMock.Object);
    }

    [Test]
    public void Validate_EmptyApiKey_LogsCriticalAndReturnsSuccessWithoutThrowing()
    {
        // ARRANGE - an empty ApiKey is unsafe, but the warning hook must keep the app running.
        var options = new CustomerEndpointsSecurityOptions { ApiKey = "" };

        // ACT
        var result = _cut.Validate(null, options);

        // ASSERT
        result.Should().Be(ValidateOptionsResult.Success);
        VerifyCriticalLogged(Times.Once());
    }

    [Test]
    public void Validate_DefaultApiKey_LogsCriticalAndReturnsSuccessWithoutThrowing()
    {
        // ARRANGE - the shipped/known default is unsafe, but the warning hook must keep the app running.
        var options = new CustomerEndpointsSecurityOptions { ApiKey = "verySecureApiKey" };

        // ACT
        var result = _cut.Validate(null, options);

        // ASSERT
        result.Should().Be(ValidateOptionsResult.Success);
        VerifyCriticalLogged(Times.Once());
    }

    [Test]
    public void Validate_SecureApiKey_ReturnsSuccessWithoutLogging()
    {
        // ARRANGE
        var options = new CustomerEndpointsSecurityOptions { ApiKey = "a-long-random-secret" };

        // ACT
        var result = _cut.Validate(null, options);

        // ASSERT
        result.Should().Be(ValidateOptionsResult.Success);
        VerifyCriticalLogged(Times.Never());
    }

    private void VerifyCriticalLogged(Times times)
    {
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}