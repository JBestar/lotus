namespace LotusOddEven.Core.Models;

/// <summary>
/// One game round: left open card + right hidden (bet) card.
/// </summary>
public sealed class RoundResult
{
    public int RoundId { get; init; }
    public required CardModel OpenCard { get; init; }
    public CardModel? HiddenCard { get; set; }
    public int ShoeRound { get; init; } = 1;
    public bool IsShoeStart { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public bool IsSettled => HiddenCard is not null;

    public BetSide? UnderOverResult => HiddenCard?.UnderOver;
    public BetSide? RedBlackResult => HiddenCard?.RedBlack;
}
