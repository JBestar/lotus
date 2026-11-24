using System.IO;
using System.Text.Json;
using LotusOddEven.Core.Models;

namespace LotusOddEven.App.Services;

/// <summary>
/// Persists algorithm profiles (표준 + user-named categories) as JSON.
/// </summary>
public sealed class AlgorithmCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public AlgorithmCatalogService(string? filePath = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LotusOddEven");
        Directory.CreateDirectory(dir);
        _filePath = filePath ?? Path.Combine(dir, "algorithm-profiles.json");
    }

    public List<AlgorithmProfile> LoadOrCreateDefaults()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var state = JsonSerializer.Deserialize<AlgorithmCatalogState>(json, JsonOptions);
                if (state?.Profiles is { Count: > 0 })
                {
                    EnsureBuiltIns(state.Profiles);
                    return state.Profiles;
                }
            }
            catch
            {
                // fall through to defaults
            }
        }

        var defaults = CreateDefaults();
        Save(defaults);
        return defaults;
    }

    public void Save(IEnumerable<AlgorithmProfile> profiles)
    {
        var state = new AlgorithmCatalogState { Profiles = profiles.ToList() };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    public static List<AlgorithmProfile> CreateDefaults() =>
    [
        new AlgorithmProfile
        {
            Name = "표준",
            Algorithm = BettingAlgorithmKind.Martingale,
            IsBuiltIn = true,
            Amounts = [1000, 2000, 4000, 8000, 16000, 32000, 64000, 128000]
        },
        new AlgorithmProfile
        {
            Name = "표준",
            Algorithm = BettingAlgorithmKind.Cruise,
            IsBuiltIn = true,
            Amounts = [1000, 1000, 2000, 2000, 4000, 4000, 8000, 8000]
        }
    ];

    private static void EnsureBuiltIns(List<AlgorithmProfile> profiles)
    {
        foreach (BettingAlgorithmKind kind in Enum.GetValues<BettingAlgorithmKind>())
        {
            if (profiles.Any(p => p.Algorithm == kind && p.Name == "표준"))
                continue;
            profiles.Insert(0, CreateDefaults().First(p => p.Algorithm == kind));
        }
    }
}
