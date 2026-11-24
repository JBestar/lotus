using LotusOddEven.Core.Models;

namespace LotusOddEven.Core.Services;

public static class CardStreamBuilder
{
    /// <summary>
    /// Time-ordered stream:
    /// past open → past hidden → … → previous open → previous hidden → [current open].
    /// </summary>
    public static List<CardModel> Build(
        IReadOnlyList<RoundResult> history,
        CardModel? currentOpen = null)
    {
        var stream = new List<CardModel>();
        foreach (var round in history)
        {
            stream.Add(round.OpenCard);
            if (round.HiddenCard is not null)
                stream.Add(round.HiddenCard);
        }

        if (currentOpen is not null)
            stream.Add(currentOpen);

        return stream;
    }
}

public sealed class PatternDetector
{
    public PatternHit? Detect(IReadOnlyList<CardModel> stream, BetAxis axis, int n)
    {
        if (n < 1)
            throw new ArgumentOutOfRangeException(nameof(n));
        if (stream.Count < n)
            return null;

        var window = stream.Skip(stream.Count - n).ToList();
        var first = window[0].Attribute(axis);
        if (window.Any(c => c.Attribute(axis) != first))
            return null;

        return new PatternHit
        {
            Axis = axis,
            StreakSide = first,
            BetSide = first.Opposite(),
            StreakLength = n,
            Cards = window
        };
    }

    public PatternHit? DetectFirst(
        IReadOnlyList<CardModel> stream,
        IEnumerable<BetAxis> axes,
        int n)
    {
        foreach (var axis in axes)
        {
            var hit = Detect(stream, axis, n);
            if (hit is not null)
                return hit;
        }

        return null;
    }
}
