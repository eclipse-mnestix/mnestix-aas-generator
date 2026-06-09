namespace MnestixCore.RequiredShellsAssertion.Interfaces;

public interface IRequiredShellsAssertion
{
    Task AssertRequiredShellsAsync(CancellationToken cancellationToken = default);
}