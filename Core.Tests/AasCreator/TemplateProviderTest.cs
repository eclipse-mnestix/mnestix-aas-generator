using FluentAssertions;
using MnestixCore.AasCreator.Templates;
using MnestixCore.Dtos;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasCreator;

public class TemplateProviderTest
{
    private static readonly AasIds TestAasIds = new(
        "https://example.com/assetId123",
        "assetId123",
        "https://example.com/aas/assetId123",
        "aas_assetId123");

    [Test]
    public void GetAas_WithThumbnail_InjectsDefaultThumbnail()
    {
        // ARRANGE
        var options = new AasCreationOptions
        {
            DefaultThumbnail = new DefaultThumbnail
            {
                Path = "https://example.com/logo.png",
                ContentType = "image/png"
            }
        };

        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options);

        // ASSERT
        var json = JObject.Parse(result);
        var defaultThumbnail = json["assetInformation"]?["defaultThumbnail"];
        defaultThumbnail.Should().NotBeNull();
        defaultThumbnail?["path"]?.ToString().Should().Be("https://example.com/logo.png");
        defaultThumbnail?["contentType"]?.ToString().Should().Be("image/png");
    }

    [Test]
    public void GetAas_WithThumbnailNoContentType_OmitsContentType()
    {
        // ARRANGE
        var options = new AasCreationOptions
        {
            DefaultThumbnail = new DefaultThumbnail
            {
                Path = "https://example.com/logo.png",
                ContentType = null
            }
        };

        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options);

        // ASSERT
        var json = JObject.Parse(result);
        var defaultThumbnail = json["assetInformation"]?["defaultThumbnail"];
        defaultThumbnail.Should().NotBeNull();
        defaultThumbnail?["path"]?.ToString().Should().Be("https://example.com/logo.png");
        defaultThumbnail?["contentType"].Should().BeNull();
    }

    [Test]
    public void GetAas_WithThumbnailEmptyContentType_OmitsContentType()
    {
        // ARRANGE
        var options = new AasCreationOptions
        {
            DefaultThumbnail = new DefaultThumbnail
            {
                Path = "https://example.com/logo.png",
                ContentType = ""
            }
        };

        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options);

        // ASSERT
        var json = JObject.Parse(result);
        var defaultThumbnail = json["assetInformation"]?["defaultThumbnail"];
        defaultThumbnail.Should().NotBeNull();
        defaultThumbnail?["path"]?.ToString().Should().Be("https://example.com/logo.png");
        defaultThumbnail?["contentType"].Should().BeNull();
    }

    [Test]
    public void GetAas_WithNullThumbnail_NoDefaultThumbnailInJson()
    {
        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options: null);

        // ASSERT
        var json = JObject.Parse(result);
        json["assetInformation"]?["defaultThumbnail"].Should().BeNull();
    }

    [Test]
    public void GetAas_DefaultAssetKind_IsInstance()
    {
        // ACT
        var result = TemplateProvider.GetAas(TestAasIds);

        // ASSERT
        var json = JObject.Parse(result);
        json["assetInformation"]?["assetKind"]?.ToString().Should().Be("Instance");
    }

    [Test]
    public void GetAas_WithTypeAssetKind_InjectsType()
    {
        // ARRANGE
        var options = new AasCreationOptions { AssetKind = AssetKind.Type };

        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options);

        // ASSERT
        var json = JObject.Parse(result);
        json["assetInformation"]?["assetKind"]?.ToString().Should().Be("Type");
    }

    [Test]
    public void GetAas_WithNotApplicableAssetKind_InjectsNotApplicable()
    {
        // ARRANGE
        var options = new AasCreationOptions { AssetKind = AssetKind.NotApplicable };

        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options);

        // ASSERT
        var json = JObject.Parse(result);
        json["assetInformation"]?["assetKind"]?.ToString().Should().Be("NotApplicable");
    }

    [Test]
    public void GetAas_WithThumbnailAndTypeAssetKind_InjectsBoth()
    {
        // ARRANGE
        var options = new AasCreationOptions
        {
            DefaultThumbnail = new DefaultThumbnail
            {
                Path = "https://example.com/logo.png",
                ContentType = "image/png"
            },
            AssetKind = AssetKind.Type
        };

        // ACT
        var result = TemplateProvider.GetAas(TestAasIds, options);

        // ASSERT
        var json = JObject.Parse(result);
        json["assetInformation"]?["assetKind"]?.ToString().Should().Be("Type");
        var defaultThumbnail = json["assetInformation"]?["defaultThumbnail"];
        defaultThumbnail.Should().NotBeNull();
        defaultThumbnail?["path"]?.ToString().Should().Be("https://example.com/logo.png");
    }
}
