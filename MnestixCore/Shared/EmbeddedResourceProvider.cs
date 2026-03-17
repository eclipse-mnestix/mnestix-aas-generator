using System.Reflection;

namespace MnestixCore.Shared;

public static class EmbeddedResourceProvider
{
    public static string GetEmbeddedResourceContent(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        
        using var stream = asm.GetManifestResourceStream(asm.GetName().Name + "." + resourceName);
        
        if (stream == null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static byte[] GetEmbeddedResourceBytes(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(asm.GetName().Name + "." + resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Resource '{resourceName}' not found in assembly '{asm.GetName().Name}'.");
        }
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}