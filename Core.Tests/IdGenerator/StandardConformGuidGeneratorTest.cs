using MnestixCore.IdGenerator;
using FluentAssertions;

namespace Core.Tests.IdGenerator;

public class StandardConformGuidGeneratorTest
{
    [Test]
    public void GenerateStandardConformGuid_Execute_ReturnsGuidWithAllowedValues()
    {
        // ACT
        var generatedGuid = StandardConformGuidGenerator.GenerateStandardConformGuid();

        // ASSERT
        generatedGuid.Should().NotContainAny("-", ",", ".", "-", "/", "\"", "@", "!", "^", "[", "]", "{", "}", "|", "<",
            ">", ";", ":", "-", "_", "?", "=", "`", "´", "&", "%", "$", "§", "Ö", "ö", "ä", "Ä", "Ü", "ü");
        generatedGuid.Length.Should().Be(32);
    }
}