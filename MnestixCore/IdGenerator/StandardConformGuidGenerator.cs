namespace MnestixCore.IdGenerator;

public class StandardConformGuidGenerator
{
    /// <summary>
    /// Generates a guid with a length of 32. 
    /// </summary>
    /// <returns>Random generated guid.</returns>
    public static string GenerateStandardConformGuid()
    {
        return Guid.NewGuid().ToString().Replace("-", string.Empty);
    }
}