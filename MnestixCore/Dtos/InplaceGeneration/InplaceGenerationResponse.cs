namespace MnestixCore.Dtos.InplaceGeneration;

public class InplaceGenerationResponse
{
    public IEnumerable<InplaceGenerationResult> Results { get; init; } = Enumerable.Empty<InplaceGenerationResult>();
}
