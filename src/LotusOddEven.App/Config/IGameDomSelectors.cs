namespace LotusOddEven.App.Config;

/// <summary>
/// CSS/DOM selectors for the live game page.
/// Replace values when the site markup changes — keep parsing logic untouched.
/// </summary>
public interface IGameDomSelectors
{
    string TimerSelector { get; }
    string OpenCardSelector { get; }
    string HiddenCardSelector { get; }
    string ShuffleIndicatorSelector { get; }
    string RoundIdSelector { get; }

    string BetUnderButtonSelector { get; }
    string BetOverButtonSelector { get; }
    string BetRedButtonSelector { get; }
    string BetBlackButtonSelector { get; }
    string ChipAmountInputSelector { get; }

    /// <summary>
    /// Bead-plate / odd-even circle cells used for shoe-start border overlay.
    /// </summary>
    string BeadPlateCellSelector { get; }
}

/// <summary>
/// Placeholder selectors — update to match the real Lotus Odd/Even DOM.
/// </summary>
public sealed class DefaultGameDomSelectors : IGameDomSelectors
{
    public string TimerSelector { get; set; } = ".timer, .countdown, #game-timer";
    public string OpenCardSelector { get; set; } = ".open-card, .card-left, #open-card";
    public string HiddenCardSelector { get; set; } = ".hidden-card, .card-right, #hidden-card";
    public string ShuffleIndicatorSelector { get; set; } = ".shuffle, .shoe-reset, [data-shuffle='true']";
    public string RoundIdSelector { get; set; } = ".round-no, #round-id";

    public string BetUnderButtonSelector { get; set; } = "[data-bet='under'], .bet-under";
    public string BetOverButtonSelector { get; set; } = "[data-bet='over'], .bet-over";
    public string BetRedButtonSelector { get; set; } = "[data-bet='red'], .bet-red";
    public string BetBlackButtonSelector { get; set; } = "[data-bet='black'], .bet-black";
    public string ChipAmountInputSelector { get; set; } = "input.bet-amount, #chip-amount";

    // Typical bead-plate cells — adjust after inspecting the live site DOM.
    public string BeadPlateCellSelector { get; set; } =
        ".bead-plate .cell, .roadmap .bead, .odd-even-board .circle, [data-bead='true']";
}

