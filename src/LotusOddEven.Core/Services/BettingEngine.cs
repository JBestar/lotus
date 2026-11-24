using LotusOddEven.Core.Models;

namespace LotusOddEven.Core.Services;

/// <summary>
/// Pure betting state machine — no UI / WebView dependencies.
/// </summary>
public sealed class BettingEngine
{
    private readonly PatternDetector _detector = new();
    private RoundResult? _pendingRound;
    private BetDecision? _pendingDecision;
    private bool _betPlacedThisRound;

    public BettingSettings Settings { get; private set; } = new();

    public EnginePhase Phase { get; private set; } = EnginePhase.Idle;
    public int CurrentStep { get; private set; } = 1;
    public BetSide? ActiveSide { get; private set; }
    public BetAxis? ActiveAxis { get; private set; }

    /// <summary>Board number inside the current shoe (1..13).</summary>
    public int CurrentShoeRound { get; private set; } = 1;

    public int ShoeIndex { get; private set; } = 1;

    public IReadOnlyList<RoundResult> History => _history;
    private readonly List<RoundResult> _history = new();

    public BetDecision? LastDecision { get; private set; }
    public SettleResult? LastSettle { get; private set; }
    public IReadOnlyList<string> Log => _log;
    private readonly List<string> _log = new();

    public void UpdateSettings(BettingSettings settings)
    {
        settings.Validate();
        Settings = settings;
        LogMsg($"설정 갱신: N={settings.TargetStreakCount}, 단계마무리={settings.IsStepFinishEnabled}/{settings.StepFinishRound}");
    }

    /// <summary>
    /// Step-finish gate from the product spec.
    /// </summary>
    public static bool CanPlaceBet(
        int currentShoeRound,
        int currentStep,
        int stepFinishRound,
        bool isStepFinishEnabled)
    {
        if (!isStepFinishEnabled)
            return true;
        if (currentStep > 1)
            return true;
        if (currentStep == 1)
            return currentShoeRound <= stepFinishRound;
        return false;
    }

    public bool CanPlaceBet() =>
        CanPlaceBet(CurrentShoeRound, CurrentStep, Settings.StepFinishRound, Settings.IsStepFinishEnabled);

    /// <summary>
    /// Shuffle / manual reset → 1판. Martingale step is preserved.
    /// </summary>
    public void OnShuffleDetected()
    {
        var prev = CurrentShoeRound;
        CurrentShoeRound = 1;
        ShoeIndex++;
        LogMsg($"셔플 감지 (이전 {prev}판) → shoe#{ShoeIndex} 1판 (step={CurrentStep} 유지)");
    }

    /// <summary>Manual toolbar action: force current board as 1판.</summary>
    public void ResetShoeToRoundOne()
    {
        CurrentShoeRound = 1;
        ShoeIndex++;
        LogMsg($"수동 슈 리셋 → shoe#{ShoeIndex} 1판 (step={CurrentStep} 유지)");
    }

    public void SetShoeRound(int round)
    {
        if (round is < 1 or > 13)
            throw new ArgumentOutOfRangeException(nameof(round));
        CurrentShoeRound = round;
    }

    /// <summary>
    /// Called when the open card is known (betting window 35s~0s).
    /// </summary>
    public BetDecision BeginRound(int roundId, CardModel openCard)
    {
        if (_pendingRound is { IsSettled: false })
            throw new InvalidOperationException("이전 회차가 아직 정산되지 않았습니다. SettleHidden을 먼저 호출하세요.");

        var pending = new RoundResult
        {
            RoundId = roundId,
            OpenCard = openCard,
            ShoeRound = CurrentShoeRound,
            IsShoeStart = CurrentShoeRound == 1
        };
        _pendingRound = pending;
        _betPlacedThisRound = false;

        var decision = DecideBet(openCard);
        LastDecision = decision;
        _pendingDecision = decision.Place ? decision : null;
        return decision;
    }

    public void ConfirmBetPlaced()
    {
        if (_pendingDecision is not { Place: true })
            throw new InvalidOperationException("확정할 대기 베팅이 없습니다.");

        _betPlacedThisRound = true;
        Phase = EnginePhase.Active;
        ActiveSide = _pendingDecision.Side;
        ActiveAxis = _pendingDecision.Axis;
        CurrentStep = _pendingDecision.Step;
        LogMsg($"베팅 확정: {ActiveSide} step={CurrentStep} amount={_pendingDecision.Amount}");
    }

    public SettleResult SettleHidden(CardModel hiddenCard, bool shuffleAfter = false)
    {
        if (_pendingRound is null)
            throw new InvalidOperationException("정산할 열린 회차가 없습니다.");

        var round = _pendingRound;
        round.HiddenCard = hiddenCard;
        _history.Add(round);

        SettleResult result;
        if (_betPlacedThisRound && ActiveSide is not null && ActiveAxis is not null)
            result = SettleActiveBet(hiddenCard);
        else
        {
            result = new SettleResult
            {
                Outcome = BetOutcome.Skip,
                Won = false,
                Side = null,
                StepBefore = CurrentStep,
                StepAfter = CurrentStep,
                Amount = 0,
                PhaseAfter = Phase,
                Message = "이번 회차 베팅 없음"
            };
        }

        LastSettle = result;
        _pendingRound = null;
        _pendingDecision = null;
        _betPlacedThisRound = false;

        if (shuffleAfter)
            OnShuffleDetected();
        else if (CurrentShoeRound == round.ShoeRound)
            CurrentShoeRound++;

        return result;
    }

    public (BetDecision Decision, SettleResult Settle) PlayRound(
        int roundId,
        CardModel openCard,
        CardModel hiddenCard,
        bool autoConfirm = true,
        bool shuffleAfter = false)
    {
        var decision = BeginRound(roundId, openCard);
        if (autoConfirm && decision.Place)
            ConfirmBetPlaced();
        var settle = SettleHidden(hiddenCard, shuffleAfter);
        return (decision, settle);
    }

    public void SeedHistory(IEnumerable<RoundResult> rounds)
    {
        foreach (var r in rounds)
        {
            if (r.HiddenCard is null)
                throw new ArgumentException("Seed rounds must be settled.");
            _history.Add(r);
        }
    }

    public void ClearHistory() => _history.Clear();

    public EngineSnapshot Snapshot() => new()
    {
        Phase = Phase,
        CurrentStep = CurrentStep,
        ActiveSide = ActiveSide,
        ActiveAxis = ActiveAxis,
        ShoeRound = CurrentShoeRound,
        ShoeIndex = ShoeIndex,
        PendingBet = _betPlacedThisRound,
        HistoryCount = _history.Count
    };

    public IReadOnlyList<CardModel> GetCardStream(CardModel? includeCurrentOpen = null) =>
        CardStreamBuilder.Build(_history, includeCurrentOpen);

    private BetDecision DecideBet(CardModel openCard)
    {
        var shoeRound = CurrentShoeRound;

        if (Phase == EnginePhase.Active && ActiveSide is not null)
        {
            if (!CanPlaceBet())
            {
                return new BetDecision
                {
                    Place = false,
                    Reason = "단계마무리: 진행 베팅이 차단됨",
                    ShoeRound = shoeRound
                };
            }

            return new BetDecision
            {
                Place = true,
                Reason = $"마틴 계속 step={CurrentStep}",
                Side = ActiveSide,
                Axis = ActiveAxis,
                Step = CurrentStep,
                Amount = Settings.AmountForStep(CurrentStep),
                ShoeRound = shoeRound
            };
        }

        if (Settings.EnabledAxes.Count == 0)
        {
            return new BetDecision
            {
                Place = false,
                Reason = "베팅 타입이 선택되지 않음",
                ShoeRound = shoeRound
            };
        }

        if (!CanPlaceBet())
        {
            return new BetDecision
            {
                Place = false,
                Reason = $"단계마무리: {shoeRound}판 > M={Settings.StepFinishRound}, 신규 진입 스킵",
                ShoeRound = shoeRound
            };
        }

        var stream = CardStreamBuilder.Build(_history, openCard);
        var hit = _detector.DetectFirst(stream, Settings.EnabledAxes, Settings.TargetStreakCount);
        if (hit is null)
        {
            return new BetDecision
            {
                Place = false,
                Reason = $"{Settings.TargetStreakCount}연속 패턴 없음",
                ShoeRound = shoeRound
            };
        }

        return new BetDecision
        {
            Place = true,
            Reason = $"신규 진입: {hit.StreakLength}연속 {hit.StreakSide} → {hit.BetSide}",
            Side = hit.BetSide,
            Axis = hit.Axis,
            Step = 1,
            Amount = Settings.AmountForStep(1),
            Pattern = hit,
            ShoeRound = shoeRound
        };
    }

    private SettleResult SettleActiveBet(CardModel hiddenCard)
    {
        var stepBefore = CurrentStep;
        var amount = Settings.AmountForStep(stepBefore);
        var actual = hiddenCard.Attribute(ActiveAxis!.Value);
        var won = actual == ActiveSide;

        if (won)
        {
            var side = ActiveSide;
            CurrentStep = 1;
            Phase = EnginePhase.Idle;
            ActiveSide = null;
            ActiveAxis = null;
            var msg = $"적중 WIN step={stepBefore} ({side}); 대기 전환";
            LogMsg(msg);
            return new SettleResult
            {
                Outcome = BetOutcome.Win,
                Won = true,
                Side = side,
                StepBefore = stepBefore,
                StepAfter = 1,
                Amount = amount,
                PhaseAfter = EnginePhase.Idle,
                Message = msg
            };
        }

        if (stepBefore >= Settings.MaxStep)
        {
            var side = ActiveSide;
            CurrentStep = 1;
            Phase = EnginePhase.Idle;
            ActiveSide = null;
            ActiveAxis = null;
            var msg = $"최고단계 패배 LOSE step={stepBefore} ({side}); 사이클 종료";
            LogMsg(msg);
            return new SettleResult
            {
                Outcome = BetOutcome.Lose,
                Won = false,
                Side = side,
                StepBefore = stepBefore,
                StepAfter = 1,
                Amount = amount,
                PhaseAfter = EnginePhase.Idle,
                Message = msg
            };
        }

        CurrentStep = stepBefore + 1;
        Phase = EnginePhase.Active;
        var loseMsg = $"미적중 LOSE step={stepBefore} ({ActiveSide}); → step={CurrentStep}";
        LogMsg(loseMsg);
        return new SettleResult
        {
            Outcome = BetOutcome.Lose,
            Won = false,
            Side = ActiveSide,
            StepBefore = stepBefore,
            StepAfter = CurrentStep,
            Amount = amount,
            PhaseAfter = EnginePhase.Active,
            Message = loseMsg
        };
    }

    private void LogMsg(string message) => _log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
}
