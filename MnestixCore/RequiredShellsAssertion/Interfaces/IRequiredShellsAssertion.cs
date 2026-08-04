namespace MnestixCore.RequiredShellsAssertion.Interfaces;

public interface IRequiredShellsAssertion
{
    /// <summary>
    /// Assures that all required AAS are stored in the repository.
    /// </summary>
    /// <param name="addExampleAas">
    /// If false, demo/example shells (e.g. 'lni0729', 'Mnestix') are skipped.
    /// Configuration, DefaultTemplate and CustomTemplate are always checked.
    /// </param>
    Task AssertRequiredShellsAsync(bool addExampleAas = true);
}