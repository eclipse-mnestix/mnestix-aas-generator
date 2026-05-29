namespace MnestixCore.TemplateBuilder;

public enum BlueprintValidationRule
{
    InvalidQualifierSegmentCount,
    EmptyMappingExpression,
    UnknownFieldName,
    FieldNotApplicableToModelType,
    UnsupportedModelType,
    DuplicateMappingField,
    MlpValueAndMultiLanguageConflict,
    InvalidJsonataSyntax,
    EmptyFilterExpression,
    InvalidFilterJsonataSyntax,
    EmptyCollectionPath,
    InvalidCollectionJsonPath,
    CollectionPathMissingWildcard,
    InvalidCollectionParentModelType,
    InvalidCardinalityValue
}
