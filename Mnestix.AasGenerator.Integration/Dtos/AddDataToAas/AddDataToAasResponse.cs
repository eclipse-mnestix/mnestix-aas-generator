using MnestixCore.AasGenerator;

namespace MnestixCore.Dtos.AddDataToAas;

public class AddDataToAasResponse
{
    public IEnumerable<AasGeneratorResult> Results { get; init; } = Enumerable.Empty<AasGeneratorResult>();
}