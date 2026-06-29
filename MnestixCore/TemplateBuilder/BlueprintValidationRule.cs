using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MnestixCore.TemplateBuilder;

[JsonConverter(typeof(StringEnumConverter))]
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
    InvalidCardinalityValue,
    FieldRequiresCollectionScope
}
