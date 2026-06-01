using System.Diagnostics;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder;
using MnestixCore.TemplateBuilder.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

public class AasGeneratorTests
{
    private MnestixCore.AasGenerator.AasGenerator _aasGenerator = null!;
    private IDataMapper _dataToInstanceMapper = null!;
    private Mock<IRepoProxyClient> _repoProxyClientMock = null!;
    private Mock<IBlueprintProvider> _templateSubmodelsProviderMock = null!;
    private Mock<IAasIdGeneratorService> _idGeneratorMock = null!;
    private Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>> _loggerMock = null!;
    private readonly IOptions<RepoProxyOptions> _repoProxyOptions = Options.Create(new RepoProxyOptions());
    private const string SubmodelTemplatePath = "AasGenerator/TestJsons/CustomTemplateSubmodelWithMappingInfo.json";
    private const string NewSubmodelId = "TheNewSubmodelId";
    private const string TestSubmodelPath = "/submodels";
    private const string TestAasPath = "/aas";
    private const string TestBase64EncodedAasId = "dGVzdEFhc0lk"; // base64 encoded "testAasId"

    [SetUp]
    public void SetUp()
    {
        _dataToInstanceMapper = new DataMapper(new BlueprintValidator());
        _repoProxyClientMock = new Mock<IRepoProxyClient>();
        _templateSubmodelsProviderMock = new Mock<IBlueprintProvider>();
        _idGeneratorMock = new Mock<IAasIdGeneratorService>();
        _loggerMock = new Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>>();
        
        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = TestAasPath,
            SubmodelPath = TestSubmodelPath,
        };
        
        _aasGenerator = new MnestixCore.AasGenerator.AasGenerator(
            _dataToInstanceMapper,
            _repoProxyClientMock.Object,
            _templateSubmodelsProviderMock.Object,
            _idGeneratorMock.Object,
            Options.Create(repoProxyOptions),
            _loggerMock.Object);

        _idGeneratorMock.Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>())).ReturnsAsync(new List<string> { NewSubmodelId });
    }

    [Test]
    public async Task AddDataToAasAsync_WhenGivenAnEmptyListOfTemplateIds_ReturnsEmptyListOfResults()
    {
        // ARRANGE
        const string language = "de";
        const string aasId = "";
        var templateIds = Array.Empty<string>();
        var dataJson = new JObject();

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(aasId, templateIds, dataJson, language);

        // ASSERT
        result.Should().BeEquivalentTo(Array.Empty<AasGeneratorResult>());
    }

            [Test]
    public async Task AddDataToAasAsync_MandatoryAndOptionalField_Success()
    {
        await RunDataIngestTest("MandatoryAndOptionalField");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputOnlyMandatoryField_Success()
    {
        await RunDataIngestTest("InputOnlyMandatoryField");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputOnlyOptionalField_ShouldFail()
    {
        await RunDataIngestFailureTest("InputOnlyOptionalField");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputList_Success()
    {
        await RunDataIngestTest("InputList");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputNestedList_Success()
    {
        await RunDataIngestTest("InputNestedList");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithMandatoryListElementMissing_ShouldFail()
    {
        await RunDataIngestFailureTest("InputListWithMandatoryListElementMissing");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithMandatoryListMissing_ShouldFail()
    {
        await RunDataIngestFailureTest("InputListWithMandatoryListMissing");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithOptionalListElementMissing_Success()
    {
        await RunDataIngestTest("InputListWithOptionalListElementMissing");
    }
    
    [Test]
    public async Task AddDataToAasAsync_InputListWithOptionalListMissing_Success()
    {
        await RunDataIngestTest("InputListWithOptionalListMissing");
    }

    [Test, Ignore("Performance test depends on Hardware")]
    public async Task AddDataToAasAsync_InputList_PerformanceWith10kElements()
    {
        await RunPerformanceTestWith10kElements();
    }

    [Test]
    public async Task AddDataToAasAsync_InputFilter_Success()
    {
        await RunDataIngestTest("InputFilter");
    }

    [Test]
    public async Task AddDataToAasAsync_InputSMLWithIdShorts_Success()
    {
        await RunDataIngestTest("InputSMLWithIdShorts");
    }

    [Test]
    public async Task AddDataToAasAsync_InputSMLWithoutIdShorts_Success()
    {
        await RunDataIngestTest("InputSMLWithoutIdShorts");
    }

    [Test]
    public async Task AddDataToAasAsync_InputSMLNestedMixed_Success()
    {
        await RunDataIngestTest("InputSMLNestedMixed");
    }

    [Test]
    public async Task AddDataToAasAsync_InputJsonataExpressions_Success()
    {
        await RunDataIngestTest("InputJsonataExpressions");
    }

    [Test]
    public async Task AddDataToAasAsync_InputJsonataBacktickEscapedFields_Success()
    {
        await RunDataIngestTest("InputJsonataBacktickEscapedFields");
    }

    [Test]
    public async Task AddDataToAasAsync_InputJsonataExpressions_NonExistingFn_ShouldFail()
    {
        await RunDataIngestFailureTest("InputInvalidJsonataExpressions_NonExistingFn");
    }

    [Test]
    public async Task AddDataToAasAsync_InputJsonataExpressions_InvalidStringLen_ShouldFail()
    {
        await RunDataIngestFailureTest("InputInvalidJsonataExpressions_InvalidStringLen");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldGlobalAssetId_Success()
    {
        await RunDataIngestTest("InputMultiFieldGlobalAssetId");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldIdShort_Success()
    {
        await RunDataIngestTest("InputMultiFieldIdShort");
    }

    [Test]
    public async Task AddDataToAasAsync_InputIdShortSanitization_Success()
    {
        await RunDataIngestTest("InputIdShortSanitization");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldMappingLegacy_Success()
    {
        await RunDataIngestTest("InputMultiFieldMappingLegacy");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldEntityType_Success()
    {
        await RunDataIngestTest("InputMultiFieldEntityType");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldDisplayName_Success()
    {
        await RunDataIngestTest("InputMultiFieldDisplayName");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldRelationship_Success()
    {
        await RunDataIngestTest("InputMultiFieldRelationship");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldDuplicate_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMultiFieldDuplicate");
    }

    [Test]
    public async Task AddDataToAasAsync_InputHierarchicalStructures_Success()
    {
        await RunDataIngestTest("InputHierarchicalStructures");
    }

    [Test]
    public async Task AddDataToAasAsync_InputValueTypeValidationSuccess_Success()
    {
        await RunDataIngestTest("InputValueTypeValidationSuccess");
    }

    [Test]
    public async Task AddDataToAasAsync_InputValueTypeValidationFailure_ShouldFail()
    {
        await RunDataIngestFailureTest("InputValueTypeValidationFailure");
    }

    [Test]
    public async Task AddDataToAasAsync_InputValueTypeUnknown_Success()
    {
        await RunDataIngestTest("InputValueTypeUnknown");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiLanguagePropertyValidationSuccess_Success()
    {
        await RunDataIngestTest("InputMultiLanguagePropertyValidationSuccess");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiLanguagePropertyValidationFailure_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMultiLanguagePropertyValidationFailure");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPValidationFailureArray_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMLPValidationFailureArray");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPValidationFailureNestedArray_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMLPValidationFailureNestedArray");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPValidationSuccessInteger_Success()
    {
        await RunDataIngestTest("InputMLPValidationSuccessInteger");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPValidationSuccessBoolean_Success()
    {
        await RunDataIngestTest("InputMLPValidationSuccessBoolean");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPValidationSuccessFloat_Success()
    {
        await RunDataIngestTest("InputMLPValidationSuccessFloat");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPValidationSuccessNull_Success()
    {
        await RunDataIngestTest("InputMLPValidationSuccessNull");
    }

    [Test]
    public async Task AddDataToAasAsync_InputPropertyValueObjectRejected_ShouldFail()
    {
        await RunDataIngestFailureTest("InputPropertyValueObjectRejected");
    }

    [Test]
    public async Task AddDataToAasAsync_InputPropertyValueArrayRejected_ShouldFail()
    {
        await RunDataIngestFailureTest("InputPropertyValueArrayRejected");
    }

    [Test]
    public async Task AddDataToAasAsync_InputPropertyValueAbsent_Success()
    {
        await RunDataIngestTest("InputPropertyValueAbsent");
    }

    // --- MLP multiLanguage tests (MNE-357) ---

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_Success()
    {
        await RunDataIngestTest("InputMLPMultiLanguage_Success");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_SingleLang_Success()
    {
        await RunDataIngestTest("InputMLPMultiLanguage_SingleLang");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_NonStringValues_Success()
    {
        await RunDataIngestTest("InputMLPMultiLanguage_NonStringValues");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_OverridesDefault_Success()
    {
        await RunDataIngestTest("InputMLPMultiLanguage_OverridesDefault");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_FailureScalar_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMLPMultiLanguage_FailureScalar");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_FailureArray_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMLPMultiLanguage_FailureArray");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_EmptyObject_Optional_Success()
    {
        await RunDataIngestTest("InputMLPMultiLanguage_EmptyObject_Optional");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_EmptyObject_Mandatory_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMLPMultiLanguage_EmptyObject_Mandatory");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_MissingPath_Optional_Success()
    {
        await RunDataIngestTest("InputMLPMultiLanguage_MissingPath_Optional");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPLegacy_NoLanguage_ShouldFail()
    {
        await RunDataIngestFailureTest("InputMLPLegacy_NoLanguage_Fails", language: null);
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPLegacy_WithLanguage_StillWorks()
    {
        // Regression test - existing MLP behavior with explicit language still works
        await RunDataIngestTest("InputMultiLanguagePropertyValidationSuccess");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPLegacy_MappingInfo_WithLanguage_Success()
    {
        // Legacy SMT/MappingInfo (no /value suffix) on MLP still works with language param
        await RunDataIngestTest("InputMLPLegacy_MappingInfo_WithLanguage");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPLegacy_MappingInfoValue_WithLanguage_Success()
    {
        // Legacy SMT/MappingInfo/value on MLP still works with language param
        await RunDataIngestTest("InputMLPLegacy_MappingInfoValue_WithLanguage");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_EmptyStringSkipped_Success()
    {
        // Empty string values for a language key should not be mapped into the MLP
        await RunDataIngestTest("InputMLPMultiLanguage_EmptyStringSkipped");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_NullValueSkipped_Success()
    {
        // Null values for a language key should not be mapped into the MLP
        await RunDataIngestTest("InputMLPMultiLanguage_NullValueSkipped");
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_AllEmptyStrings_Mandatory_ShouldFail()
    {
        // All values are empty strings → no valid entries → treated as empty → mandatory fails
        await RunDataIngestFailureTest("InputMLPMultiLanguage_AllEmptyStrings_Mandatory");
    }

    private async Task RunDataIngestTest(string testCaseName)
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel(testCaseName);
        var templateData = DataIngestTestFileProvider.GetData(testCaseName);
        var expectedResult = DataIngestTestFileProvider.GetExpectedResult(testCaseName);
        
        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };
        
        string? capturedSubmodelContent = null;
        
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestSubmodelPath), It.IsAny<string>()))
            .Callback<string, string>((path, content) => capturedSubmodelContent = content)
            .ReturnsAsync("created");
            
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestAasPath), It.IsAny<string>()))
            .ReturnsAsync("created");
        
        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);
        
        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });
        
        // This method is only for success cases - expectedResult should not be null
        expectedResult.Should().NotBeNull($"Test case '{testCaseName}' should have a valid expected result for success test");
        
        // The real implementation will process the template and data according to the mapping rules
        
        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, "en");
        
        // ASSERT
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Success.Should().BeTrue();
        
        capturedSubmodelContent.Should().NotBeNull();
        var actualSubmodel = JObject.Parse(capturedSubmodelContent!);
        
        JToken.DeepEquals(actualSubmodel, expectedResult).Should().BeTrue(
            $"Test case '{testCaseName}' failed: Expected submodel content to match expected result \n Expected: {expectedResult}\n Actual: {actualSubmodel}");
    }
    
    private async Task RunDataIngestFailureTest(string testCaseName, string? language = "en")
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel(testCaseName);
        var templateData = DataIngestTestFileProvider.GetData(testCaseName);
        var expectedResult = DataIngestTestFileProvider.GetExpectedResult(testCaseName);
        
        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };
        
        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);
        
        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });
        
        // This method is only for failure cases - expectedResult should be null
        expectedResult.Should().BeNull($"Test case '{testCaseName}' should have null expected result for failure test");
        
        // The real implementation will throw SubmodelDataToInstanceMapperException when validation fails
        
        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, language);
        
        // ASSERT
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Success.Should().BeFalse($"Test case '{testCaseName}' should fail due to missing mandatory data");
    }
    
    private async Task RunPerformanceTestWith10kElements()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("InputList");
        
        // Create test data with 10,000 contact persons and pets
        var contactPersons = new List<object>();
        var pets = new List<object>();
        
        for (int i = 0; i < 10000; i++)
        {
            contactPersons.Add(new
            {
                name = $"ContactPerson_{i}",
                email = $"person_{i}@example.com"
            });
            
            pets.Add(new
            {
                name = $"Pet_{i}",
                typeOfAnimal = i % 2 == 0 ? "Dog" : "Cat"
            });
        }
        
        var templateData = JObject.FromObject(new
        {
            sourceData = new
            {
                contactPersons = contactPersons,
                pets = pets
            }
        });
        
        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };
        
        string? capturedSubmodelContent = null;
        
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestSubmodelPath), It.IsAny<string>()))
            .Callback<string, string>((path, content) => capturedSubmodelContent = content)
            .ReturnsAsync("created");
            
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.Is<string>(path => path == TestAasPath), It.IsAny<string>()))
            .ReturnsAsync("created");
        
        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);
        
        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });
        
        // ACT - Measure execution time
        var stopwatch = Stopwatch.StartNew();
        var result = await _aasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, "en");
        stopwatch.Stop();
        
        // ASSERT
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Success.Should().BeTrue();
        
        capturedSubmodelContent.Should().NotBeNull();
        var actualSubmodel = JObject.Parse(capturedSubmodelContent!);
        
        // Verify that we processed all 10,000 elements
        var contactPersonsArray = actualSubmodel.SelectToken("$.submodelElements[?(@.idShort=='ContactPersons')].value") as JArray;
        var petsArray = actualSubmodel.SelectToken("$.submodelElements[?(@.idShort=='Pets')].value") as JArray;
        
        contactPersonsArray.Should().NotBeNull();
        contactPersonsArray!.Count.Should().Be(10000, "Should have processed all 10,000 contact persons");
        
        petsArray.Should().NotBeNull();
        petsArray!.Count.Should().Be(10000, "Should have processed all 10,000 pets");
        
        // Log performance results
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var elementsPerSecond = (20000.0 / elapsedMs) * 1000; // 20k total elements (10k contacts + 10k pets)
        
        TestContext.WriteLine($"Performance Test Results:");
        TestContext.WriteLine($"- Processed 20,000 elements (10,000 contact persons + 10,000 pets)");
        TestContext.WriteLine($"- Execution time: {elapsedMs} ms ({stopwatch.Elapsed.TotalSeconds:F2} seconds)");
        TestContext.WriteLine($"- Throughput: {elementsPerSecond:F0} elements/second");
        
        // Assert performance requirement (adjust threshold as needed)
        elapsedMs.Should().BeLessThan(30000, "Processing 20,000 elements should complete within 30 seconds");
    }

    #region Workflow Logging Tests

    // T003: Successful generation with debug=true returns DebugInfo.Logs from all workflow phases
    [Test]
    public async Task AddDataToAasAsync_DebugTrue_ReturnsWorkflowLogsFromAllPhases()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: true);

        // ASSERT
        var first = result.First();
        first.Success.Should().BeTrue();
        first.DebugInfo.Should().NotBeNull();
        first.DebugInfo!.Logs.Should().NotBeNull();
        first.DebugInfo.Logs!.Should().NotBeEmpty();

        var allLogs = string.Join("\n", first.DebugInfo.Logs!);
        allLogs.Should().Contain("Fetching blueprint");
        allLogs.Should().Contain("Blueprint fetched successfully");
        allLogs.Should().Contain("Generating submodel ID");
        allLogs.Should().Contain("Starting data mapping");
        allLogs.Should().Contain("Data mapping completed");
        allLogs.Should().Contain("Posting submodel to repository");
        allLogs.Should().Contain("Submodel reference added to shell");
    }

    // T004: Successful generation with debug=false returns null DebugInfo
    [Test]
    public async Task AddDataToAasAsync_DebugFalse_ReturnsNullDebugInfo()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: false);

        // ASSERT
        var first = result.First();
        first.Success.Should().BeTrue();
        first.DebugInfo.Should().BeNull();
    }

    // T005: Multiple blueprints with debug=true returns independent log trails
    [Test]
    public async Task AddDataToAasAsync_MultipleBlueprintsDebugTrue_ReturnsIndependentLogTrails()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:Blueprint1", "urn:smtemplate:Blueprint2" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var results = (await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: true)).ToList();

        // ASSERT
        results.Should().HaveCount(2);
        results[0].DebugInfo.Should().NotBeNull();
        results[1].DebugInfo.Should().NotBeNull();

        var logs0 = string.Join("\n", results[0].DebugInfo!.Logs!);
        var logs1 = string.Join("\n", results[1].DebugInfo!.Logs!);

        logs0.Should().Contain("Blueprint1");
        logs1.Should().Contain("Blueprint2");
    }

    // T013: Blueprint fetch failure returns ErrorInfo.Logs with retrieval attempt entry
    [Test]
    public async Task AddDataToAasAsync_BlueprintFetchFails_ReturnsErrorInfoWithLogs()
    {
        // ARRANGE
        var templateIds = new List<string> { "urn:smtemplate:NonExistent" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Not found"));

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, new JObject(), "en");

        // ASSERT
        var first = result.First();
        first.Success.Should().BeFalse();
        first.ErrorInfo.Should().NotBeNull();
        first.ErrorInfo!.Logs.Should().NotBeNull();

        var allLogs = string.Join("\n", first.ErrorInfo.Logs!);
        allLogs.Should().Contain("Fetching blueprint");
        allLogs.Should().Contain("Blueprint fetch failed");
    }

    // T014: ID generation failure returns ErrorInfo.Logs with blueprint success + ID failure
    [Test]
    public async Task AddDataToAasAsync_IdGenerationFails_ReturnsErrorInfoWithLogs()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ThrowsAsync(new Exception("ID service down"));

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, new JObject(), "en");

        // ASSERT
        var first = result.First();
        first.Success.Should().BeFalse();
        first.ErrorInfo.Should().NotBeNull();
        first.ErrorInfo!.Logs.Should().NotBeNull();

        var allLogs = string.Join("\n", first.ErrorInfo.Logs!);
        allLogs.Should().Contain("Blueprint fetched successfully");
        allLogs.Should().Contain("Generating submodel ID");
        allLogs.Should().Contain("Submodel ID generation failed");
    }

    // T015: Data mapping failure returns ErrorInfo.Logs with preceding steps + preserves Qualifier
    [Test]
    public async Task AddDataToAasAsync_DataMappingFails_ReturnsErrorInfoWithLogsAndPreservesQualifier()
    {
        // ARRANGE - use a test case known to fail mapping
        await RunDataIngestFailureTest("InputOnlyOptionalField");

        // The RunDataIngestFailureTest already validates Success=false.
        // Re-run with debug to check error logs:
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("InputOnlyOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("InputOnlyOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en");
        var first = result.First();

        first.Success.Should().BeFalse();
        first.ErrorInfo.Should().NotBeNull();
        first.ErrorInfo!.Logs.Should().NotBeNull();

        var allLogs = string.Join("\n", first.ErrorInfo.Logs!);
        allLogs.Should().Contain("Blueprint fetched successfully");
        allLogs.Should().Contain("Starting data mapping");
        allLogs.Should().Contain("Data mapping failed");
    }

    // T016: Repo persistence failure returns ErrorInfo.Logs with all preceding step entries
    [Test]
    public async Task AddDataToAasAsync_RepoPersistenceFails_ReturnsErrorInfoWithLogs()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new MnestixCore.Errors.RepoProxyException(MnestixCore.Errors.ErrorCodes.CouldNotPutSubmodel, "Repository unavailable"));

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en");

        // ASSERT
        var first = result.First();
        first.Success.Should().BeFalse();
        first.ErrorInfo.Should().NotBeNull();
        first.ErrorInfo!.Logs.Should().NotBeNull();

        var allLogs = string.Join("\n", first.ErrorInfo.Logs!);
        allLogs.Should().Contain("Blueprint fetched successfully");
        allLogs.Should().Contain("Submodel ID generated");
        allLogs.Should().Contain("Data mapping completed");
        allLogs.Should().Contain("Posting submodel to repository");
        allLogs.Should().Contain("Repository operation failed");
    }

    // T024: Error results include DebugInfo.Logs when debug=true
    [Test]
    public async Task AddDataToAasAsync_ErrorWithDebugTrue_ReturnsDebugInfoWithLogs()
    {
        // ARRANGE
        var templateIds = new List<string> { "urn:smtemplate:NonExistent" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Not found"));

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, new JObject(), "en", debug: true);

        // ASSERT
        var first = result.First();
        first.Success.Should().BeFalse();
        first.DebugInfo.Should().NotBeNull();
        first.DebugInfo!.Logs.Should().NotBeNull();
        first.DebugInfo.Logs!.Should().NotBeEmpty();

        var allLogs = string.Join("\n", first.DebugInfo.Logs!);
        allLogs.Should().Contain("Fetching blueprint");
        allLogs.Should().Contain("Blueprint fetch failed");
    }

    // T025: Preamble appears as the first log entry when provided
    [Test]
    public async Task AddDataToAasAsync_WithPreamble_PreambleIsFirstLogEntry()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: true, preamble: "Called by integration test XYZ");

        // ASSERT
        var first = result.First();
        first.DebugInfo.Should().NotBeNull();
        first.DebugInfo!.Logs.Should().NotBeNull();
        first.DebugInfo.Logs!.First().Should().Contain("Called by integration test XYZ");
    }

    // T026: No preamble provided means first log is the blueprint mapping entry
    [Test]
    public async Task AddDataToAasAsync_WithoutPreamble_FirstLogIsBlueprintMapping()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: true, preamble: null);

        // ASSERT
        var first = result.First();
        first.DebugInfo.Should().NotBeNull();
        first.DebugInfo!.Logs.Should().NotBeNull();
        first.DebugInfo.Logs!.First().Should().Contain("Mapping blueprint");
    }

    // T027: Error with debug=false still returns null DebugInfo
    [Test]
    public async Task AddDataToAasAsync_ErrorWithDebugFalse_ReturnsNullDebugInfo()
    {
        // ARRANGE
        var templateIds = new List<string> { "urn:smtemplate:NonExistent" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Not found"));

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, new JObject(), "en", debug: false);

        // ASSERT
        var first = result.First();
        first.Success.Should().BeFalse();
        first.DebugInfo.Should().BeNull();
        first.ErrorInfo.Should().NotBeNull();
        first.ErrorInfo!.Logs.Should().NotBeEmpty();
    }

    // T023: All log entries match the format pattern SEVERITY [timestamp] - message
    [Test]
    public async Task AddDataToAasAsync_DebugTrue_AllLogEntriesMatchFormatConvention()
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var templateData = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: true);

        // ASSERT
        var first = result.First();
        first.DebugInfo.Should().NotBeNull();
        first.DebugInfo!.Logs.Should().NotBeNull();

        var pattern = new System.Text.RegularExpressions.Regex(@"^(INFO|WARNING|ERROR) \[.+\] - .+$");
        foreach (var log in first.DebugInfo.Logs!)
        {
            pattern.IsMatch(log).Should().BeTrue($"Log entry '{log}' should match SEVERITY [timestamp] - message format");
        }
    }

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguage_OverridesDefault_LogsWarning()
    {
        // ARRANGE — template has a default value [{en:"Default Company"}] that gets overridden
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel("InputMLPMultiLanguage_OverridesDefault");
        var templateData = DataIngestTestFileProvider.GetData("InputMLPMultiLanguage_OverridesDefault");
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("created");

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(TestBase64EncodedAasId, templateIds, templateData, "en", debug: true);

        // ASSERT
        var first = result.First();
        first.Success.Should().BeTrue();
        first.DebugInfo.Should().NotBeNull();
        first.DebugInfo!.Logs.Should().NotBeNull();

        var allLogs = string.Join("\n", first.DebugInfo.Logs!);
        allLogs.Should().Contain("template default for 'value' was overridden by mapped data");
    }

    #endregion

    #region Blueprint Validation at Generation-Time Tests

    [Test]
    public async Task AddDataToAasAsync_InputMLPMultiLanguageQualifier_OnProperty_ShouldFailWithValidationErrors()
    {
        await RunBlueprintValidationFailureTest("InputMLPMultiLanguageQualifier_OnProperty_Fails",
            BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldInvalidField_ShouldFailWithValidationErrors()
    {
        await RunBlueprintValidationFailureTest("InputMultiFieldInvalidField",
            BlueprintValidationRule.UnknownFieldName);
    }

    [Test]
    public async Task AddDataToAasAsync_InputMultiFieldTypeMismatch_ShouldFailWithValidationErrors()
    {
        await RunBlueprintValidationFailureTest("InputMultiFieldTypeMismatch",
            BlueprintValidationRule.FieldNotApplicableToModelType);
    }

    [Test]
    public async Task AddDataToAasAsync_InputWhitespaceOnlyExpression_ShouldFailWithValidationErrors()
    {
        await RunBlueprintValidationFailureTest("InputWhitespaceOnlyExpression_Fails",
            BlueprintValidationRule.EmptyMappingExpression);
    }

    [Test]
    public async Task AddDataToAasAsync_InputListWithMandatoryEmptyArray_ShouldFail()
    {
        await RunDataIngestFailureTest("InputListWithMandatoryEmptyArray");
    }

    private async Task RunBlueprintValidationFailureTest(string testCaseName, BlueprintValidationRule expectedRule)
    {
        // ARRANGE
        var templateSubmodel = DataIngestTestFileProvider.GetTemplateSubmodel(testCaseName);
        var templateData = DataIngestTestFileProvider.GetData(testCaseName);

        var aasId = "TestAasId";
        var templateIds = new List<string> { "urn:smtemplate:DemoTemplate" };

        _templateSubmodelsProviderMock
            .Setup(x => x.GetBlueprintAsync(It.IsAny<string>()))
            .ReturnsAsync(templateSubmodel);

        _idGeneratorMock
            .Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { "TheNewSubmodelId" });

        // ACT
        var result = await _aasGenerator.AddDataToAasAsync(aasId, templateIds, templateData, "en");

        // ASSERT
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var first = result.First();
        first.Success.Should().BeFalse();
        first.ValidationErrors.Should().NotBeNull();
        first.ValidationErrors.Should().Contain(e => e.Rule == expectedRule);
    }

    #endregion
}