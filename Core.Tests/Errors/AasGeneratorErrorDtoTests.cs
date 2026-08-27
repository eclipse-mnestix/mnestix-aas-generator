using FluentAssertions;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.Tests.Errors;

public class AasGeneratorErrorDtoTests
{
    [TestCase(AasGeneratorErrorCode.MappingFailed, "MappingFailed")]
    [TestCase(AasGeneratorErrorCode.BlueprintValidationFailed, "BlueprintValidationFailed")]
    [TestCase(AasGeneratorErrorCode.RepositoryOperationFailed, "RepositoryOperationFailed")]
    [TestCase(AasGeneratorErrorCode.InvalidBlueprint, "InvalidBlueprint")]
    [TestCase(AasGeneratorErrorCode.UnknownError, "UnknownError")]
    public void AasGeneratorErrorCode_SerializesToString(AasGeneratorErrorCode code, string expectedString)
    {
        var dto = new AasGeneratorErrorDto(code, "some message", null);

        var json = JObject.Parse(JsonConvert.SerializeObject(dto));

        json["Code"]!.Value<string>().Should().Be(expectedString);
    }
}
