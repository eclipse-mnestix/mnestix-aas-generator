namespace MnestixCore.AasGenerator.Interfaces;

public interface IPipelineStep<TContext>
{
    Task<TContext> ExecuteAsync(TContext context);
}
