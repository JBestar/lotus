using CommunityToolkit.Mvvm.ComponentModel;
using LotusOddEven.Core.Models;

namespace LotusOddEven.App.ViewModels;

public partial class ResultDotItem : ObservableObject
{
    [ObservableProperty] private int _roundId;
    [ObservableProperty] private int _shoeRound;
    [ObservableProperty] private bool _isShoeStart;
    [ObservableProperty] private string _label = "?";
    [ObservableProperty] private string _tooltip = string.Empty;
    [ObservableProperty] private BetSide? _underOver;
    [ObservableProperty] private BetSide? _redBlack;

    public static ResultDotItem FromRound(RoundResult round)
    {
        var uo = round.HiddenCard?.UnderOver;
        var label = uo switch
        {
            BetSide.Under => "U",
            BetSide.Over => "O",
            _ => "?"
        };

        return new ResultDotItem
        {
            RoundId = round.RoundId,
            ShoeRound = round.ShoeRound,
            IsShoeStart = round.IsShoeStart,
            Label = label,
            UnderOver = uo,
            RedBlack = round.HiddenCard?.RedBlack,
            Tooltip = $"회차 {round.RoundId} / {round.ShoeRound}판\n" +
                      $"Open {round.OpenCard}  Hidden {round.HiddenCard}"
        };
    }
}
