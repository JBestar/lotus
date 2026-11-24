namespace LotusOddEven.Core.Models;

public sealed class PatternHit
{
    public required BetAxis Axis { get; init; }
    public required BetSide StreakSide { get; init; }
    public required BetSide BetSide { get; init; }
    public int StreakLength { get; init; }
    public required IReadOnlyList<CardModel> Cards { get; init; }
}

public sealed class BetDecision
{
    public bool Place { get; init; }
    public string Reason { get; init; } = string.Empty;
    public BetSide? Side { get; init; }
    public BetAxis? Axis { get; init; }
    public int Step { get; init; } = 1;
    public int Amount { get; init; }
    public PatternHit? Pattern { get; init; }
    public int ShoeRound { get; init; }
}

public sealed class SettleResult
{
    public BetOutcome Outcome { get; init; }
    public bool Won { get; init; }
    public BetSide? Side { get; init; }
    public int StepBefore { get; init; }
    public int StepAfter { get; init; }
    public int Amount { get; init; }
    public EnginePhase PhaseAfter { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class EngineSnapshot
{
    public EnginePhase Phase { get; init; }
    public int CurrentStep { get; init; }
    public BetSide? ActiveSide { get; init; }
    public BetAxis? ActiveAxis { get; init; }
    public int ShoeRound { get; init; }
    public int ShoeIndex { get; init; }
    public bool PendingBet { get; init; }
    public int HistoryCount { get; init; }
}
