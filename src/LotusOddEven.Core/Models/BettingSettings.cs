namespace LotusOddEven.Core.Models;

/// <summary>
/// Snapshot of UI betting settings consumed by <see cref="Services.BettingEngine"/>.
/// </summary>
public sealed class BettingSettings
{
    public bool IsUnderOverEnabled { get; set; } = true;
    public bool IsRedBlackEnabled { get; set; } = true;
    public int TargetStreakCount { get; set; } = 5;
    public bool IsStepFinishEnabled { get; set; }
    public int StepFinishRound { get; set; } = 8;
    public IList<int> MartinAmounts { get; set; } = new List<int> { 1000, 2000, 4000, 8000, 16000 };

    /// <summary>Same-round tie-break when both axes match.</summary>
    public IReadOnlyList<BetAxis> AxisPriority { get; set; } =
        new[] { BetAxis.UnderOver, BetAxis.RedBlack };

    public IReadOnlyList<BetAxis> EnabledAxes
    {
        get
        {
            var list = new List<BetAxis>();
            foreach (var axis in AxisPriority)
            {
                if (axis == BetAxis.UnderOver && IsUnderOverEnabled)
                    list.Add(axis);
                else if (axis == BetAxis.RedBlack && IsRedBlackEnabled)
                    list.Add(axis);
            }
            return list;
        }
    }

    public int MaxStep => MartinAmounts.Count;

    public void Validate()
    {
        if (TargetStreakCount < 1)
            throw new ArgumentOutOfRangeException(nameof(TargetStreakCount), "Must be >= 1.");
        if (StepFinishRound is < 1 or > 13)
            throw new ArgumentOutOfRangeException(nameof(StepFinishRound), "Must be 1..13.");
        if (MartinAmounts.Count == 0)
            throw new ArgumentException("Martin amounts must not be empty.");
        if (MartinAmounts.Any(a => a <= 0))
            throw new ArgumentException("Martin amounts must be positive.");
    }

    public int AmountForStep(int step)
    {
        if (step < 1 || step > MaxStep)
            throw new ArgumentOutOfRangeException(nameof(step), $"Step must be 1..{MaxStep}.");
        return MartinAmounts[step - 1];
    }
}
