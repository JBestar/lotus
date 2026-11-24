namespace LotusOddEven.Core.Models;

/// <summary>
/// Lotus Odd/Even card (ranks 1–10 only; A=1, no J/Q/K).
/// </summary>
public sealed class CardModel : IEquatable<CardModel>
{
    public int Rank { get; }
    public Suit Suit { get; }

    public CardModel(int rank, Suit suit)
    {
        if (rank is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be 1..10.");
        Rank = rank;
        Suit = suit;
    }

    public BetSide UnderOver => Rank <= 5 ? BetSide.Under : BetSide.Over;

    public BetSide RedBlack => Suit is Suit.Spade or Suit.Club
        ? BetSide.Black
        : BetSide.Red;

    public BetSide Attribute(BetAxis axis) => axis switch
    {
        BetAxis.UnderOver => UnderOver,
        BetAxis.RedBlack => RedBlack,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
    };

    public override string ToString()
    {
        var symbol = Suit switch
        {
            Suit.Spade => "♠",
            Suit.Club => "♣",
            Suit.Diamond => "♦",
            Suit.Heart => "♥",
            _ => "?"
        };
        return $"{Rank}{symbol}";
    }

    public bool Equals(CardModel? other) =>
        other is not null && Rank == other.Rank && Suit == other.Suit;

    public override bool Equals(object? obj) => obj is CardModel other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Rank, Suit);

    /// <summary>
    /// Parse compact tokens: "10S", "A♥", "6d", "♠9".
    /// </summary>
    public static CardModel Parse(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Empty card token.", nameof(token));

        var raw = token.Trim().ToLowerInvariant().Replace(" ", "");
        Suit? suit = null;
        string rankPart = raw;

        foreach (var (alias, mapped) in SuitAliases)
        {
            if (raw.StartsWith(alias, StringComparison.Ordinal))
            {
                suit = mapped;
                rankPart = raw[alias.Length..];
                break;
            }

            if (raw.EndsWith(alias, StringComparison.Ordinal))
            {
                suit = mapped;
                rankPart = raw[..^alias.Length];
                break;
            }
        }

        if (suit is null)
            throw new FormatException($"Cannot parse suit from: '{token}'");

        if (!RankAliases.TryGetValue(rankPart, out var rank))
            throw new FormatException($"Cannot parse rank from: '{token}'");

        return new CardModel(rank, suit.Value);
    }

    private static readonly Dictionary<string, Suit> SuitAliases = new()
    {
        ["s"] = Suit.Spade,
        ["spade"] = Suit.Spade,
        ["spades"] = Suit.Spade,
        ["♠"] = Suit.Spade,
        ["c"] = Suit.Club,
        ["club"] = Suit.Club,
        ["clubs"] = Suit.Club,
        ["♣"] = Suit.Club,
        ["d"] = Suit.Diamond,
        ["diamond"] = Suit.Diamond,
        ["diamonds"] = Suit.Diamond,
        ["♦"] = Suit.Diamond,
        ["h"] = Suit.Heart,
        ["heart"] = Suit.Heart,
        ["hearts"] = Suit.Heart,
        ["♥"] = Suit.Heart,
    };

    private static readonly Dictionary<string, int> RankAliases = new()
    {
        ["a"] = 1,
        ["1"] = 1,
        ["2"] = 2,
        ["3"] = 3,
        ["4"] = 4,
        ["5"] = 5,
        ["6"] = 6,
        ["7"] = 7,
        ["8"] = 8,
        ["9"] = 9,
        ["10"] = 10,
        ["t"] = 10,
    };
}
