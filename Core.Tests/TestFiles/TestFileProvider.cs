using System.Reflection;

namespace Core.Tests.TestFiles;

public static class TestFileProvider
{
    public static string GetIdGeneratorSettingsSubmodelWithDynamicPartValues()
    {
        return GetEmbeddedResourceContent("TestFiles.IdGeneratorSettingsSubmodelWithDynamicPartValues.json");
    }

    public static string GetIdGeneratorSettingsSubmodelWithValues()
    {
        return GetEmbeddedResourceContent("TestFiles.IdGeneratorSettingsSubmodelWithValues.json");
    }

    public static string GetIdGeneratorSettingsSubmodelWithoutValues()
    {
        return GetEmbeddedResourceContent("TestFiles.IdGeneratorSettingsSubmodelWithoutValues.json");
    }

    public static string GetTemplateSubmodelNameplate()
    {
        return GetEmbeddedResourceContent("TestFiles.DefaultTemplateSubmodelNameplate.json");
    }

    public static string GetBlueprintSubmodelNameplateReference()
    {
        return GetEmbeddedResourceContent("TestFiles.CustomTemplateSubmodelNameplateReference.json");
    }
    public static string GetTemplateSubmodelNameplateReference()
    {
        return GetEmbeddedResourceContent("TestFiles.DefaultTemplateSubmodelNameplateReference.json");
    }

    public static string GetBlueprintSubmodelNameplate()
    {
        return GetEmbeddedResourceContent("TestFiles.CustomTemplateSubmodelNameplate.json");
    }

    /// <summary>
    ///     Holds two transformed submodels
    ///     - nameplate 1.0
    ///     - nameplate 1.1
    /// </summary>
    public static string GetTwoBlueprintsTransformedForRepo()
    {
        return GetEmbeddedResourceContent("TestFiles.TwoCustomSubmodelTemplatesTransformedForRepo.json");
    }

    public static string GetExampleAasJson()
    {
        return GetEmbeddedResourceContent("TestFiles.ExampleAas.json");
    }

    public static string GetExampleBlueprintJson()
    {
        return GetEmbeddedResourceContent("TestFiles.NameplateBlueprint.json");
    }

    private static string GetEmbeddedResourceContent(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream(asm.GetName().Name + "." + resourceName);
        if (stream == null) return string.Empty;

        var source = new StreamReader(stream);
        var fileContent = source.ReadToEnd();
        source.Dispose();
        stream.Dispose();
        return fileContent;
    }
}