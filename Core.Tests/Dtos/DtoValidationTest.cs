using FluentAssertions;
using MnestixCore.Dtos;
using Newtonsoft.Json;

namespace Core.Tests.Dtos;

public class DtoValidationTest
{
    [Test]
    public void AdministrativeInformation_MissingVersion_ThrowsJsonSerializationException()
    {
        // ARRANGE
        var json = "{\"revision\": \"2\"}";

        // ACT
        Action act = () => JsonConvert.DeserializeObject<AdministrativeInformation>(json);

        // ASSERT
        act.Should().Throw<JsonSerializationException>()
            .WithMessage("*version*");
    }

    [Test]
    public void AdministrativeInformation_WithVersion_Succeeds()
    {
        // ARRANGE
        var json = "{\"version\": \"1.0\", \"revision\": \"2\"}";

        // ACT
        var result = JsonConvert.DeserializeObject<AdministrativeInformation>(json);

        // ASSERT
        result.Should().NotBeNull();
        result!.Version.Should().Be("1.0");
        result.Revision.Should().Be("2");
    }

    [Test]
    public void SpecificAssetId_MissingName_ThrowsJsonSerializationException()
    {
        // ARRANGE
        var json = "{\"value\": \"12345\"}";

        // ACT
        Action act = () => JsonConvert.DeserializeObject<SpecificAssetId>(json);

        // ASSERT
        act.Should().Throw<JsonSerializationException>()
            .WithMessage("*name*");
    }

    [Test]
    public void SpecificAssetId_MissingValue_ThrowsJsonSerializationException()
    {
        // ARRANGE
        var json = "{\"name\": \"SerialNumber\"}";

        // ACT
        Action act = () => JsonConvert.DeserializeObject<SpecificAssetId>(json);

        // ASSERT
        act.Should().Throw<JsonSerializationException>()
            .WithMessage("*value*");
    }

    [Test]
    public void SpecificAssetId_WithBothFields_Succeeds()
    {
        // ARRANGE
        var json = "{\"name\": \"SerialNumber\", \"value\": \"12345\"}";

        // ACT
        var result = JsonConvert.DeserializeObject<SpecificAssetId>(json);

        // ASSERT
        result.Should().NotBeNull();
        result!.Name.Should().Be("SerialNumber");
        result.Value.Should().Be("12345");
    }
}
