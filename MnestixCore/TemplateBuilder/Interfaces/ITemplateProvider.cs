using Newtonsoft.Json.Linq;

namespace MnestixCore.TemplateBuilder.Interfaces;

public interface ITemplateProvider
{
    /// <summary>
    /// Provides all submodels from the template AAS
    /// </summary>
    /// <returns>All submodel as json array.</returns>
    public Task<JArray> GetAllTemplateSubmodelsAsync();
}