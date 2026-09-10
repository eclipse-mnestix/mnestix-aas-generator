using FluentAssertions;
using MnestixCore.TemplateBuilder;
using Newtonsoft.Json.Linq;

namespace Core.Tests.TemplateBuilder;

[TestFixture]
public class BlueprintValidatorTests
{
    private BlueprintValidator _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new BlueprintValidator();
    }

    #region Helpers

    private static JObject MakeBlueprint(params JToken[] submodelElements)
    {
        return new JObject
        {
            ["idShort"] = "TestBlueprint",
            ["id"] = "urn:example:blueprint:1",
            ["modelType"] = "Submodel",
            ["submodelElements"] = new JArray(submodelElements)
        };
    }

    private static JObject MakeElement(string modelType, string idShort, params JObject[] qualifiers)
    {
        var element = new JObject
        {
            ["modelType"] = modelType,
            ["idShort"] = idShort
        };
        if (qualifiers.Length > 0)
        {
            element["qualifiers"] = new JArray(qualifiers);
        }
        return element;
    }

    private static JObject MakeQualifier(string type, string? value)
    {
        var q = new JObject { ["type"] = type };
        if (value != null)
            q["value"] = value;
        return q;
    }

    private static JObject MakeSmc(string idShort, JToken[] children, params JObject[] qualifiers)
    {
        var smc = new JObject
        {
            ["modelType"] = "SubmodelElementCollection",
            ["idShort"] = idShort,
            ["value"] = new JArray(children)
        };
        if (qualifiers.Length > 0)
        {
            smc["qualifiers"] = new JArray(qualifiers);
        }
        return smc;
    }

    #endregion

    #region Happy Path

    [Test]
    public void Validate_ValidBlueprintWithMappingInfo_ReturnsNoErrors()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temperature",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.temperature"),
                MakeQualifier("SMT/Cardinality", "One"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_ValidBlueprintWithBareMappingInfo_ReturnsNoErrors()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temperature",
                MakeQualifier("MnestixAASGenerator/MappingInfo", "$.temperature"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_BlueprintWithNoQualifiers_ReturnsNoErrors()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "StaticValue")
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_ValidCollectionMapping_ReturnsNoErrors()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"),
            MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.items[*].name"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_ValidFilterMapping_ReturnsNoErrors()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "ConditionalProp",
                MakeQualifier("MnestixAASGenerator/FilterMappingInfo", "$.active = true"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.value"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 1: InvalidQualifierSegmentCount

    [Test]
    public void Validate_QualifierTypeWithFourSegments_ReturnsInvalidSegmentCountError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value/extra", "$.temp"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidQualifierSegmentCount);
    }

    [Test]
    public void Validate_QualifierTypeWithFiveSegments_ReturnsInvalidSegmentCountError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value/extra/more", "$.temp"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidQualifierSegmentCount);
    }

    #endregion

    #region Rule 2: EmptyMappingExpression

    [Test]
    public void Validate_MappingInfoWithEmptyValue_ReturnsEmptyExpressionError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", ""))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyMappingExpression);
    }

    [Test]
    public void Validate_MappingInfoWithNullValue_ReturnsEmptyExpressionError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", null))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyMappingExpression);
    }

    [Test]
    public void Validate_BareMappingInfoWithEmptyValue_ReturnsEmptyExpressionError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo", ""))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyMappingExpression);
    }

    [Test]
    public void Validate_MappingInfoWithWhitespaceOnlyValue_ReturnsEmptyExpressionError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "   "))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyMappingExpression);
    }

    #endregion

    #region Rule 3: UnknownFieldName

    [Test]
    public void Validate_UnknownFieldName_ReturnsUnknownFieldNameError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/foobar", "$.temp"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.UnknownFieldName);
    }

    [Test]
    public void Validate_AnotherUnknownField_ReturnsUnknownFieldNameError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Entity", "MyEntity",
                MakeQualifier("MnestixAASGenerator/MappingInfo/nonexistent", "$.data"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.UnknownFieldName);
    }

    #endregion

    #region Rule 4: FieldNotApplicableToModelType

    [Test]
    public void Validate_FirstFieldOnProperty_ReturnsFieldNotApplicableError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/first", "$.ref"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public void Validate_ValueFieldOnEntity_ReturnsFieldNotApplicableError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Entity", "MyEntity",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.data"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public void Validate_GlobalAssetIdOnProperty_ReturnsFieldNotApplicableError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/globalAssetId", "$.assetId"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public void Validate_MultiLanguageOnProperty_ReturnsFieldNotApplicableError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/multiLanguage", "$.translations"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public void Validate_EntityTypeOnRelationship_ReturnsFieldNotApplicableError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("RelationshipElement", "Rel",
                MakeQualifier("MnestixAASGenerator/MappingInfo/entityType", "$.type"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public void Validate_FirstFieldOnRelationship_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("RelationshipElement", "Rel",
                MakeQualifier("MnestixAASGenerator/MappingInfo/first", "$.refA"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 5: UnsupportedModelType

    [Test]
    public void Validate_MappingOnUnsupportedModelType_ReturnsUnsupportedModelTypeError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Operation", "DoSomething",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.data"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.UnsupportedModelType);
    }

    [Test]
    public void Validate_MappingOnBasicEventElement_ReturnsUnsupportedModelTypeError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("BasicEventElement", "Event",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.event"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.UnsupportedModelType);
    }

    #endregion

    #region Rule 6: DuplicateMappingField

    [Test]
    public void Validate_TwoValueMappingsOnSameElement_ReturnsDuplicateFieldError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.temp1"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.temp2"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.DuplicateMappingField);
    }

    [Test]
    public void Validate_BareAndExplicitValueOnSameElement_ReturnsDuplicateFieldError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo", "$.temp1"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.temp2"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.DuplicateMappingField);
    }

    [Test]
    public void Validate_DifferentFieldsOnSameElement_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.temp"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/idShort", "$.name"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 7: MlpValueAndMultiLanguageConflict

    [Test]
    public void Validate_MlpWithValueAndMultiLanguage_ReturnsConflictError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("MultiLanguageProperty", "Name",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.name"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/multiLanguage", "$.translations"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.MlpValueAndMultiLanguageConflict);
    }

    [Test]
    public void Validate_MlpWithBareAndMultiLanguage_ReturnsConflictError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("MultiLanguageProperty", "Name",
                MakeQualifier("MnestixAASGenerator/MappingInfo", "$.name"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/multiLanguage", "$.translations"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.MlpValueAndMultiLanguageConflict);
    }

    [Test]
    public void Validate_MlpWithOnlyMultiLanguage_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("MultiLanguageProperty", "Name",
                MakeQualifier("MnestixAASGenerator/MappingInfo/multiLanguage", "$.translations"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_MlpWithOnlyValue_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("MultiLanguageProperty", "Name",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.name"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 8: InvalidJsonataSyntax

    [Test]
    public void Validate_MappingInfoWithInvalidJsonata_ReturnsInvalidSyntaxError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.foo["))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidJsonataSyntax);
    }

    [Test]
    public void Validate_MappingInfoWithUnclosedFunction_ReturnsInvalidSyntaxError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$substring($.name, "))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidJsonataSyntax);
    }

    [Test]
    public void Validate_MappingInfoWithValidJsonata_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$substring($.name, 0, 5)"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 9: EmptyFilterExpression

    [Test]
    public void Validate_FilterMappingInfoWithEmptyValue_ReturnsEmptyFilterError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/FilterMappingInfo", ""))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyFilterExpression);
    }

    [Test]
    public void Validate_FilterMappingInfoWithNullValue_ReturnsEmptyFilterError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/FilterMappingInfo", null))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyFilterExpression);
    }

    #endregion

    #region Rule 10: InvalidFilterJsonataSyntax

    [Test]
    public void Validate_FilterWithInvalidJsonata_ReturnsInvalidFilterSyntaxError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/FilterMappingInfo", "$contains("))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidFilterJsonataSyntax);
    }

    [Test]
    public void Validate_FilterWithValidJsonata_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/FilterMappingInfo", "$.active = true"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 11: EmptyCollectionPath

    [Test]
    public void Validate_CollectionMappingInfoWithEmptyValue_ReturnsEmptyCollectionPathError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", ""));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyCollectionPath);
    }

    [Test]
    public void Validate_CollectionMappingInfoWithNullValue_ReturnsEmptyCollectionPathError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", null));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.EmptyCollectionPath);
    }

    #endregion

    #region Rule 12: InvalidCollectionJsonPath

    [Test]
    public void Validate_CollectionWithInvalidJsonPath_ReturnsInvalidPathError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$[???[*]"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidCollectionJsonPath);
    }

    #endregion

    #region Rule 13: CollectionPathMissingWildcard

    [Test]
    public void Validate_CollectionPathNotEndingWithWildcard_ReturnsMissingWildcardError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.CollectionPathMissingWildcard);
    }

    [Test]
    public void Validate_CollectionPathEndingWithWildcard_ReturnsNoError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 14: InvalidCollectionParentModelType

    [Test]
    public void Validate_CollectionMappingOnTopLevelElement_ReturnsInvalidParentError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Item",
                MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidCollectionParentModelType);
    }

    [Test]
    public void Validate_CollectionMappingWithPropertyParent_ReturnsInvalidParentError()
    {
        // Property cannot be a collection parent — only SMC/SML/Entity
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"));

        var parent = MakeElement("Property", "NotACollection");
        parent["value"] = new JArray(child);

        var blueprint = MakeBlueprint(parent);

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidCollectionParentModelType);
    }

    [Test]
    public void Validate_CollectionMappingInsideSmc_ReturnsNoError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_CollectionMappingInsideEntity_ReturnsNoError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.parts[*]"));

        var entity = new JObject
        {
            ["modelType"] = "Entity",
            ["idShort"] = "MyEntity",
            ["statements"] = new JArray(child)
        };

        var blueprint = MakeBlueprint(entity);

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Rule 15: InvalidCardinalityValue

    [Test]
    public void Validate_CardinalityWithInvalidValue_ReturnsInvalidCardinalityError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("SMT/Cardinality", "Optional"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidCardinalityValue);
    }

    [Test]
    public void Validate_CardinalityWithLowercaseOne_ReturnsInvalidCardinalityError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("SMT/Cardinality", "one"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.InvalidCardinalityValue);
    }

    [Test]
    public void Validate_CardinalityOne_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("SMT/Cardinality", "One"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_CardinalityZeroToOne_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("SMT/Cardinality", "ZeroToOne"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_CardinalityOneToMany_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("SMT/Cardinality", "OneToMany"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_CardinalityZeroToMany_ReturnsNoError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("SMT/Cardinality", "ZeroToMany"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Multiple Errors

    [Test]
    public void Validate_MultiplIssues_ReturnsAllErrors()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Prop1",
                MakeQualifier("MnestixAASGenerator/MappingInfo/foobar", "$.x")),       // UnknownFieldName
            MakeElement("Property", "Prop2",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "")),            // EmptyMappingExpression
            MakeElement("Operation", "Op1",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.data"))       // UnsupportedModelType
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().HaveCount(3);
        errors.Should().Contain(e => e.Rule == BlueprintValidationRule.UnknownFieldName);
        errors.Should().Contain(e => e.Rule == BlueprintValidationRule.EmptyMappingExpression);
        errors.Should().Contain(e => e.Rule == BlueprintValidationRule.UnsupportedModelType);
    }

    #endregion

    #region Path Building

    [Test]
    public void Validate_ErrorPath_ContainsIdShortBreadcrumb()
    {
        var child = MakeElement("Property", "NestedProp",
            MakeQualifier("MnestixAASGenerator/MappingInfo/foobar", "$.x"));

        var blueprint = MakeBlueprint(
            MakeSmc("ParentCollection", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle();
        errors[0].Path.Should().Contain("ParentCollection");
        errors[0].Path.Should().Contain("NestedProp");
    }

    #endregion

    #region Rule 16: FieldRequiresCollectionScope

    [Test]
    public void Validate_ValueTypeOutsideCollectionScope_ReturnsFieldRequiresCollectionScopeError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/valueType", "$.type"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldRequiresCollectionScope);
    }

    [Test]
    public void Validate_SemanticIdOutsideCollectionScope_ReturnsFieldRequiresCollectionScopeError()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/semanticId", "$.sid"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().ContainSingle(e => e.Rule == BlueprintValidationRule.FieldRequiresCollectionScope);
    }

    [Test]
    public void Validate_CollectionScopedFieldOnTypelessElement_NotFlaggedByRule16()
    {
        // Rule 16 is gated behind modelType != null (BlueprintValidator). A scoped field on an
        // element without a modelType is therefore not flagged. This pins that boundary.
        var typelessElement = new JObject
        {
            ["idShort"] = "Temp",
            ["qualifiers"] = new JArray(MakeQualifier("MnestixAASGenerator/MappingInfo/semanticId", "$.sid"))
        };
        var blueprint = MakeBlueprint(typelessElement);

        var errors = _sut.Validate(blueprint);

        errors.Should().NotContain(e => e.Rule == BlueprintValidationRule.FieldRequiresCollectionScope);
    }

    [Test]
    public void Validate_NonScopedFieldOutsideCollection_DoesNotTriggerRule16()
    {
        // value and idShort are not collection-scoped; Rule 16 must stay narrow and not over-fire.
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temp",
                MakeQualifier("MnestixAASGenerator/MappingInfo/value", "$.serial"),
                MakeQualifier("MnestixAASGenerator/MappingInfo/idShort", "$.name"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().NotContain(e => e.Rule == BlueprintValidationRule.FieldRequiresCollectionScope);
    }

    [Test]
    public void Validate_ValueTypeOnElementWithCollectionMappingInfo_ReturnsNoError()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"),
            MakeQualifier("MnestixAASGenerator/MappingInfo/valueType", "$.items[*].type"),
            MakeQualifier("MnestixAASGenerator/MappingInfo/semanticId", "$.items[*].sid"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_SemanticIdOnElementUnderCollectionAncestor_ReturnsNoError()
    {
        // Property carries the collection scope; a nested SMC child inherits it via ancestor walk
        var grandchild = MakeElement("Property", "Inner",
            MakeQualifier("MnestixAASGenerator/MappingInfo/semanticId", "$.items[*].sid"));
        var collectionItem = MakeSmc("Item", [grandchild],
            MakeQualifier("MnestixAASGenerator/CollectionMappingInfo", "$.items[*]"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [collectionItem])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion

    // === BACKWARD-COMPAT (legacy SMT/ prefix) — delete this whole region when SMT/ support is dropped (MNE-428) ===
    #region Backward compatibility

    [Test]
    public void Validate_LegacySmtMappingInfo_IsAcceptedLikeNewPrefix_BackwardCompat()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temperature",
                MakeQualifier("SMT/MappingInfo/value", "$.temperature"),
                MakeQualifier("SMT/Cardinality", "One"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_LegacySmtBareMappingInfo_IsAccepted_BackwardCompat()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temperature",
                MakeQualifier("SMT/MappingInfo", "$.temperature"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    [Test]
    public void Validate_LegacySmtUnknownField_ReportsUnknownFieldName_BackwardCompat()
    {
        var blueprint = MakeBlueprint(
            MakeElement("Property", "Temperature",
                MakeQualifier("SMT/MappingInfo/bogus", "$.temperature"))
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().Contain(e => e.Rule == BlueprintValidationRule.UnknownFieldName);
    }

    [Test]
    public void Validate_LegacySmtCollectionMapping_IsAccepted_BackwardCompat()
    {
        var child = MakeElement("Property", "Item",
            MakeQualifier("SMT/CollectionMappingInfo", "$.items[*]"),
            MakeQualifier("SMT/MappingInfo/value", "$.items[*].name"));

        var blueprint = MakeBlueprint(
            MakeSmc("Items", [child])
        );

        var errors = _sut.Validate(blueprint);

        errors.Should().BeEmpty();
    }

    #endregion
    // === END BACKWARD-COMPAT ===
}
