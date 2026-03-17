namespace MnestixCore.AasGenerator.Interfaces;

public interface IPipeline<TContext>
{
    Task<TContext> RunAsync(TContext context);
}
