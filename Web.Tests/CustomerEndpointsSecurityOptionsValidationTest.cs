using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MnestixApi.ApiKeyAuthorization;
using MnestixCore.Dtos.AppSettingsOptions;

namespace Web.Tests;

public class CustomerEndpointsSecurityOptionsValidationTest
{
    private CustomerEndpointsSecurityOptionsValidation _cut = null!;

    [SetUp]
    public void Setup()
    {
        _cut = new CustomerEndpointsSecurityOptionsValidation(NullLogger<CustomerEndpointsSecurityOptionsValidation>.Instance);
    }

    [Test]
    public void Validate_EmptyApiKey_ReturnsSuccessWithoutThrowing()
    {
        // ARRANGE - an empty ApiKey is unsafe, but the warning hook must keep the app running.
        var options = new CustomerEndpointsSecurityOptions { ApiKey = "" };

        // ACT
        var result = _cut.Validate(null, options);

        // ASSERT
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Test]
    public void Validate_DefaultApiKey_ReturnsSuccessWithoutThrowing()
    {
        // ARRANGE - the shipped/known default is unsafe, but the warning hook must keep the app running.
        var options = new CustomerEndpointsSecurityOptions { ApiKey = "verySecureApiKey" };

        // ACT
        var result = _cut.Validate(null, options);

        // ASSERT
        result.Should().Be(ValidateOptionsResult.Success);
    }

    [Test]
    public void Validate_SecureApiKey_ReturnsSuccess()
    {
        // ARRANGE
        var options = new CustomerEndpointsSecurityOptions { ApiKey = "a-long-random-secret" };

        // ACT
        var result = _cut.Validate(null, options);

        // ASSERT
        result.Should().Be(ValidateOptionsResult.Success);
    }
}