using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MnestixCore.RequiredShellsAssertion;
using MnestixCore.RequiredShellsAssertion.Interfaces;

namespace Mnestix.AasGenerator.DefaultTemplates;

/// <summary>
/// Registers the bundled default IDTA/Mnestix template catalogue for the AAS Generator.
/// </summary>
public static class MnestixDefaultTemplatesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bundled default catalogue and <see cref="IRequiredShellsAssertion"/>.
    /// Does not contact a repository or seed templates automatically; consumers seed explicitly
    /// by resolving <see cref="IRequiredShellsAssertion"/> and calling
    /// <see cref="IRequiredShellsAssertion.AssertRequiredShellsAsync"/> from their own startup code.
    /// </summary>
    public static IServiceCollection AddMnestixDefaultTemplates(this IServiceCollection services)
    {
        services.TryAddTransient<IRequiredShellsAssertion, RequiredShellsAssertion>();
        return services;
    }
}
