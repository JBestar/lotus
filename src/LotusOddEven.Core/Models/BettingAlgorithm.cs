using System.Text.Json.Serialization;

namespace LotusOddEven.Core.Models;

public enum BettingAlgorithmKind
{
    Martingale = 0,
    Cruise = 1
}

public sealed class AlgorithmProfile
{
    public string Name { get; set; } = "표준";
    public BettingAlgorithmKind Algorithm { get; set; } = BettingAlgorithmKind.Martingale;
    public List<int> Amounts { get; set; } = new() { 1000, 2000, 4000, 8000, 16000, 32000, 64000, 128000 };
    public bool IsBuiltIn { get; set; }

    [JsonIgnore]
    public string DisplayName => Name;
}

public sealed class AlgorithmCatalogState
{
    public List<AlgorithmProfile> Profiles { get; set; } = new();
}
