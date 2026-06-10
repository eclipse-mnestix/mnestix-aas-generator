using MnestixCore.Dtos;

namespace MnestixCore.IdGenerator.Interfaces;

public interface IMnestixConfigurationProvider
{
    /// <summary>
    /// Provides the settings for the id generation.
    /// </summary>
    /// <returns>Task which holds <see cref="IdGenerationSettings"/></returns>
    Task<IdGenerationSettings> GetIdGenerationSettingsAsync(CancellationToken cancellationToken = default);
}