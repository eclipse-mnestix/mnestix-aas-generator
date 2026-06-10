using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.TemplateBuilder;

namespace Mnestix.AasGenerator;

/// <summary>
/// Registers the pure AAS generation engine into a consumer's <see cref="IServiceCollection"/>.
/// </summary>
public static class MnestixAasGenerationCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAasGenerationEngine"/> and its pure dependencies
    /// (data mapper, blueprint validator). No options, transport, or network access.
    /// Idempotent.
    /// </summary>
    public static IServiceCollection AddMnestixAasGenerationCore(this IServiceCollection services)
    {
        services.TryAddTransient<IBlueprintValidator, BlueprintValidator>();
        services.TryAddTransient<IDataMapper, DataMapper>();
        services.TryAddTransient<IAasGenerationEngine, AasGenerationEngine>();
        return services;
    }
}
