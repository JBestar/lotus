using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LotusOddEven.Core.Models;

namespace LotusOddEven.App.ViewModels;

public partial class EditAlgorithmViewModel : ObservableObject
{
    public EditAlgorithmViewModel(BettingAlgorithmKind algorithm, AlgorithmProfile source)
    {
        Algorithm = algorithm;
        ProfileName = source.Name;
        Steps = new ObservableCollection<MartingaleStep>(
            source.Amounts.Select((amount, i) => new MartingaleStep(i + 1, amount)));
        if (Steps.Count == 0)
            Steps.Add(new MartingaleStep(1, 1000));
    }

    public BettingAlgorithmKind Algorithm { get; }

    [ObservableProperty] private string _profileName = "표준";

    public ObservableCollection<MartingaleStep> Steps { get; }

    public bool DialogResultOk { get; private set; }

    public event Action? RequestClose;

    [RelayCommand]
    private void AddStep()
    {
        var last = Steps.LastOrDefault()?.Amount ?? 1000;
        Steps.Add(new MartingaleStep(Steps.Count + 1, Math.Max(1000, last * 2)));
        Renumber();
    }

    [RelayCommand]
    private void RemoveStep()
    {
        if (Steps.Count <= 1)
            return;
        Steps.RemoveAt(Steps.Count - 1);
        Renumber();
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
            ProfileName = "표준";
        ProfileName = ProfileName.Trim();
        DialogResultOk = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultOk = false;
        RequestClose?.Invoke();
    }

    public AlgorithmProfile ToProfile()
    {
        Renumber();
        return new AlgorithmProfile
        {
            Name = ProfileName.Trim(),
            Algorithm = Algorithm,
            IsBuiltIn = ProfileName.Trim() == "표준",
            Amounts = Steps.OrderBy(s => s.Step).Select(s => s.Amount).Where(a => a > 0).ToList()
        };
    }

    private void Renumber()
    {
        for (var i = 0; i < Steps.Count; i++)
            Steps[i].Step = i + 1;
    }
}
