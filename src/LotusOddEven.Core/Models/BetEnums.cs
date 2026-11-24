namespace LotusOddEven.Core.Models;

public enum Suit
{
    Spade,   // ♠ Black
    Club,    // ♣ Black
    Diamond, // ♦ Red
    Heart    // ♥ Red
}

public enum BetAxis
{
    UnderOver,
    RedBlack
}

public enum BetSide
{
    Under,
    Over,
    Red,
    Black
}

public enum EnginePhase
{
    Idle,
    Active
}

public enum BetOutcome
{
    Win,
    Lose,
    Skip
}

public static class BetSideExtensions
{
    public static BetSide Opposite(this BetSide side) => side switch
    {
        BetSide.Under => BetSide.Over,
        BetSide.Over => BetSide.Under,
        BetSide.Red => BetSide.Black,
        BetSide.Black => BetSide.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };

    public static BetAxis Axis(this BetSide side) => side switch
    {
        BetSide.Under or BetSide.Over => BetAxis.UnderOver,
        BetSide.Red or BetSide.Black => BetAxis.RedBlack,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
    };
}
