using FluentAssertions;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.Tests.Errors;

public class AasGeneratorErrorDtoTests
{
    [TestCase(AasGeneratorErrorCode.MappingFailed, "MappingFailed")]
    [TestCase(AasGeneratorErrorCode.BlueprintValidationFailed, "BlueprintValidationFailed")]
    [TestCase(AasGeneratorErrorCode.RepositoryOperationFailed, "RepositoryOperationFailed")]
    [TestCase(AasGeneratorErrorCode.InvalidBlueprint, "InvalidBlueprint")]
    [TestCase(AasGeneratorErrorCode.InvalidInput, "InvalidInput")]
    [TestCase(AasGeneratorErrorCode.UnknownError, "UnknownError")]
    public void AasGeneratorErrorCode_SerializesToString(AasGeneratorErrorCode code, string expectedString)
    {
        var dto = new AasGeneratorErrorDto(code, "some message", null);

        var json = JObject.Parse(JsonConvert.SerializeObject(dto));

        json["Code"]!.Value<string>().Should().Be(expectedString);
    }

    [Test]
    public void AasGeneratorErrorDto_WithMappingErrorContext_SerializesContextFields()
    {
        var ctx = new MappingErrorContext("{\"type\":\"MnestixAASGenerator/MappingInfo\"}", "submodelElements[0].qualifiers[1]");
        var dto = new AasGeneratorErrorDto(AasGeneratorErrorCode.MappingFailed, "Mandatory mapping not found.", ctx);

        var json = JObject.Parse(JsonConvert.SerializeObject(dto));

        json["Context"]!["Qualifier"]!.Value<string>().Should().Be(ctx.Qualifier);
        json["Context"]!["QualifierPath"]!.Value<string>().Should().Be(ctx.QualifierPath);
    }

    [Test]
    public void AasGeneratorErrorDto_WithValidationErrorContext_SerializesErrors()
    {
        var errors = new List<BlueprintValidationError>
        {
            new(BlueprintValidationRule.EmptyMappingExpression, "submodelElements[0]", "Mapping expression is empty.")
        };
        var ctx = new ValidationErrorContext(errors);
        var dto = new AasGeneratorErrorDto(AasGeneratorErrorCode.BlueprintValidationFailed, "Validation failed.", ctx);

        var json = JObject.Parse(JsonConvert.SerializeObject(dto));

        var errorsArray = json["Context"]!["Errors"] as JArray;
        errorsArray.Should().HaveCount(1);
        errorsArray![0]["Rule"]!.Value<string>().Should().Be("EmptyMappingExpression");
    }

    [Test]
    public void SubmodelDataToInstanceMapperException_ToErrorDto_WithNullContext_ProducesMappingErrorContextWithNulls()
    {
        var ex = new SubmodelDataToInstanceMapperException("some error", context: null);

        var dto = ex.ToErrorDto();

        dto.Code.Should().Be(AasGeneratorErrorCode.MappingFailed);
        var ctx = dto.Context as MappingErrorContext;
        ctx.Should().NotBeNull();
        ctx!.Qualifier.Should().BeNull();
        ctx.QualifierPath.Should().BeNull();
    }

    [Test]
    public void BlueprintValidationException_ToErrorDto_ReturnsValidationFailedCodeWithErrors()
    {
        var errors = new List<BlueprintValidationError>
        {
            new(BlueprintValidationRule.EmptyMappingExpression, "submodelElements[0]", "Mapping expression is empty."),
            new(BlueprintValidationRule.UnknownFieldName, "submodelElements[1]", "Unknown field.")
        };
        var ex = new BlueprintValidationException(errors);

        var dto = ex.ToErrorDto();

        dto.Code.Should().Be(AasGeneratorErrorCode.BlueprintValidationFailed);
        var ctx = dto.Context as ValidationErrorContext;
        ctx.Should().NotBeNull();
        ctx!.Errors.Should().BeEquivalentTo(errors);
    }
}
