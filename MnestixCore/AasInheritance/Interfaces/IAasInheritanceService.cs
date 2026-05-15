namespace MnestixCore.AasInheritance.Interfaces;

public interface IAasInheritanceService
{
    /// <summary>
    /// Returns all asset administration shells that have a direct derivedFrom dependency on the given asset administration shell
    /// </summary>
    /// <param name="aasId">The id of the asset administration shell to search inheritors for</param>
    /// <returns>A list of <see cref="Aas"/> that derive from the given asset administration shell</returns>
    Task<List<Aas>> GetDerivedFrom(string aasId);
}