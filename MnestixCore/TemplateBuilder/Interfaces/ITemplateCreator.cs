namespace MnestixCore.TemplateBuilder.Interfaces;

public interface ITemplateCreator
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="templateSubmodel"></param>
    /// <returns></returns>
    Task AddNewSubmodelInTemplateAasAsync(string templateSubmodel, CancellationToken cancellationToken = default);
}