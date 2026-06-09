using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines;

namespace MnestixCore.Errors;

/// <summary>
/// A exception that indicates that something predictable went wrong in the process of mapping data to the template with <see cref="IDataMapper"/>.
/// Probably a required field in the json that was missing or the mapping info couldn't be found in the data json
/// </summary>
internal class SubmodelDataToInstanceMapperException : Exception
{
    public DataMappingContext? Context { get; set; }
    public SubmodelDataToInstanceMapperException()
    {
    }

    public SubmodelDataToInstanceMapperException(string? message) : base(message)
    {
    }
    public SubmodelDataToInstanceMapperException(string? message, Exception? innerException, DataMappingContext? ctx) : base(message, innerException)
    {
        Context = ctx;
    }
    public SubmodelDataToInstanceMapperException(string? message, DataMappingContext? ctx) : base(message)
    {
        Context = ctx;
    }

    public SubmodelDataToInstanceMapperException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    
}