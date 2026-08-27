using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines;
using Newtonsoft.Json;

namespace MnestixCore.Errors;

/// <summary>
/// Thrown when a predictable error occurs while mapping data to the template with <see cref="IDataMapper"/>,
/// such as a missing required field or an unresolvable mapping expression.
/// </summary>
public class SubmodelDataToInstanceMapperException : AasGeneratorException
{
    public override AasGeneratorErrorCode Code => AasGeneratorErrorCode.MappingFailed;

    public DataMappingContext? Context { get; }

    public SubmodelDataToInstanceMapperException(string message, DataMappingContext? context = null)
        : base(message)
    {
        Context = context;
    }

    public SubmodelDataToInstanceMapperException(string message, Exception innerException, DataMappingContext? context = null)
        : base(message, innerException)
    {
        Context = context;
    }

    public override AasGeneratorErrorDto ToErrorDto() =>
        new(Code, Message, new MappingErrorContext(
            Context?.Qualifier.ToString(Formatting.None),
            Context?.Qualifier.Path
        ));
}
