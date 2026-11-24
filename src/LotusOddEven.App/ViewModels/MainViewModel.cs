using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LotusOddEven.App.Services;
using LotusOddEven.App.Views;
using LotusOddEven.Core.Models;
using LotusOddEven.Core.Services;
using Microsoft.Web.WebView2.Wpf;

namespace LotusOddEven.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly BettingEngine _engine = new();
    private readonly WebViewBridgeService _bridge;
    private readonly AlgorithmCatalogService _catalog = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Random _rng = new();
    private readonly List<AlgorithmProfile> _allProfiles;
    private readonly List<int> _shoeStartBeadIndices = new();

    private int? _lastHandledRoundId;
    private string? _lastOpenToken;
    private bool _awaitingHidden;
    private int _simRoundId = 1000;
    private DateTime _sessionStarted = DateTime.Now;
    private int _beadCount;

    public MainViewModel(WebViewBridgeService bridge)
    {
        _bridge = bridge;
        _allProfiles = _catalog.LoadOrCreateDefaults();

        AlgorithmOptions = new ObservableCollection<BettingAlgorithmKind>(
            Enum.GetValues<BettingAlgorithmKind>());
        ProfileOptions = new ObservableCollection<AlgorithmProfile>();
        MartinSteps = new ObservableCollection<MartingaleStep>();
        ResultDots = new ObservableCollection<ResultDotItem>();
        Logs = new ObservableCollection<LogEntry>();
        SiteDomains = new ObservableCollection<string>
        {
            "lotuscasino.com",
            "lotus-game.com",
            "localhost"
        };

        SelectedAlgorithm = BettingAlgorithmKind.Martingale;
        RefreshProfileOptions();
        SelectedProfile = ProfileOptions.FirstOrDefault(p => p.Name == "표준") ?? ProfileOptions.FirstOrDefault();
        LoadStepsFromSelectedProfile();

        SiteDomain = SiteDomains[0];
        AddressUrl = EnsureUrl(SiteDomain);

        // Always-on features (UI checkboxes locked on)
        IsStreakSettingEnabled = true;
        IsStepFinishEnabled = true;

        ApplySettingsToEngine();
        RefreshStatusFromEngine();

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _pollTimer.Tick += async (_, _) => await PollOnceAsync();

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) =>
        {
            var span = DateTime.Now - _sessionStarted;
            ElapsedText = span.ToString(@"hh\:mm\:ss");
            RemainingTimeText = TimeSpan.FromSeconds(Math.Max(0, RemainingSeconds)).ToString(@"mm\:ss");
        };
        _elapsedTimer.Start();

        AddLog("프로그램 준비 완료. 도메인 선택 후 자동 시작 버튼을 누르세요.");
        AddLog("시뮬레이션: Ctrl+Shift+S");
    }

    public ObservableCollection<BettingAlgorithmKind> AlgorithmOptions { get; }
    public ObservableCollection<AlgorithmProfile> ProfileOptions { get; }
    public ObservableCollection<MartingaleStep> MartinSteps { get; }
    public ObservableCollection<ResultDotItem> ResultDots { get; }
    public ObservableCollection<LogEntry> Logs { get; }
    public ObservableCollection<string> SiteDomains { get; }

    [ObservableProperty] private string _appTitle = "LOTUS 홀짝 매니저 v1.3.5";
    [ObservableProperty] private string _elapsedText = "00:00:00";
    [ObservableProperty] private string _addressUrl = "https://www.google.com";

    [ObservableProperty] private string _siteDomain = "lotuscasino.com";
    [ObservableProperty] private string _siteUserId = "";
    [ObservableProperty] private string _sitePassword = "";
    [ObservableProperty] private bool _isUiLocked;

    [ObservableProperty] private bool _isBotRunning;
    [ObservableProperty] private bool _isPaused;

    [ObservableProperty] private bool _isUnderOverEnabled = true;
    [ObservableProperty] private bool _isRedBlackEnabled = true;
    [ObservableProperty] private bool _isStreakSettingEnabled = true;
    [ObservableProperty] private int _targetStreakCount = 5;
    [ObservableProperty] private bool _isStepFinishEnabled = true;
    [ObservableProperty] private int _stepFinishRound = 8;

    [ObservableProperty] private BettingAlgorithmKind _selectedAlgorithm;
    [ObservableProperty] private AlgorithmProfile? _selectedProfile;

    [ObservableProperty] private int _currentRoundId;
    [ObservableProperty] private int _currentShoeRound = 1;
    [ObservableProperty] private int _remainingSeconds = 60;
    [ObservableProperty] private string _remainingTimeText = "01:00";
    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private string _phaseText = "대기";
    [ObservableProperty] private string _activeSideText = "-";
    [ObservableProperty] private string _nextBetText = "-";
    [ObservableProperty] private string _currentStepText = "1단계";
    [ObservableProperty] private string _currentShoeText = "1판";

    [ObservableProperty] private decimal _balance = 1_000_000;
    [ObservableProperty] private decimal _profit;
    [ObservableProperty] private decimal _targetProfit = 100_000;
    [ObservableProperty] private decimal _stopLoss = 100_000;
    [ObservableProperty] private decimal _totalWagered;

    public void AttachWebView(WebView2 webView) => _bridge.Attach(webView);

    partial void OnSiteDomainChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            AddressUrl = EnsureUrl(value);
    }

    partial void OnSelectedAlgorithmChanged(BettingAlgorithmKind value)
    {
        RefreshProfileOptions();
        SelectedProfile = ProfileOptions.FirstOrDefault(p => p.Name == "표준")
                          ?? ProfileOptions.FirstOrDefault();
    }

    partial void OnSelectedProfileChanged(AlgorithmProfile? value)
    {
        LoadStepsFromSelectedProfile();
        ApplySettingsToEngine();
    }

    [RelayCommand]
    private async Task NavigateAsync()
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(SiteDomain) ? AddressUrl : EnsureUrl(SiteDomain);
            AddressUrl = url;
            await _bridge.NavigateAsync(url);
            AddLog($"사이트 이동: {url}");
        }
        catch (Exception ex)
        {
            AddLog($"이동 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RefreshPage()
    {
        _bridge.Reload();
        AddLog("새로고침");
    }

    [RelayCommand]
    private void ResetShoe()
    {
        _engine.ResetShoeToRoundOne();
        _shoeStartBeadIndices.Add(_beadCount); // next bead will be shoe start; mark current index for next settle
        RefreshStatusFromEngine();
        AddLog("수동 슈 리셋 → 다음 결과가 1판으로 표시됩니다.");
        _ = RefreshShoeOverlayAsync();
    }

    [RelayCommand]
    private async Task StartBotAsync()
    {
        IsPaused = false;
        IsBotRunning = true;
        ApplySettingsToEngine();
        try
        {
            if (!string.IsNullOrWhiteSpace(SiteDomain))
            {
                AddressUrl = EnsureUrl(SiteDomain);
                await _bridge.NavigateAsync(AddressUrl);
            }
        }
        catch (Exception ex)
        {
            AddLog($"사이트 연결 실패: {ex.Message}");
        }

        _pollTimer.Start();
        AddLog("자동 시작");
    }

    [RelayCommand]
    private void PauseBot()
    {
        if (!IsBotRunning)
            return;
        IsPaused = !IsPaused;
        AddLog(IsPaused ? "일시정지" : "재개");
    }

    [RelayCommand]
    private void StopBot()
    {
        IsBotRunning = false;
        IsPaused = false;
        _pollTimer.Stop();
        AddLog("정지");
    }

    [RelayCommand]
    private void ApplyMoneyLimits()
    {
        AddLog($"수익종료/손실종료 적용: 수익종료 {TargetProfit:N0} / 손실종료 {StopLoss:N0}");
    }

    [RelayCommand]
    private void ApplyBalance()
    {
        AddLog($"보유금액 기준 설정: {Balance:N0}");
    }

    [RelayCommand]
    private void ToggleLock()
    {
        IsUiLocked = !IsUiLocked;
        AddLog(IsUiLocked ? "UI 잠금" : "UI 잠금 해제");
    }

    [RelayCommand]
    private void ChangePassword()
    {
        AddLog("비밀번호 변경 기능은 추후 연동 예정입니다.");
        MessageBox.Show("비밀번호 변경 기능은 추후 연동 예정입니다.", AppTitle,
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void MinimizeWindow()
    {
        if (Application.Current.MainWindow is { } w)
            w.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    private void ToggleMaximizeWindow()
    {
        if (Application.Current.MainWindow is not { } w)
            return;
        w.WindowState = w.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    [RelayCommand]
    private void CloseWindow()
    {
        Application.Current.MainWindow?.Close();
    }

    [RelayCommand]
    private void EditAlgorithm()
    {
        var source = SelectedProfile ?? new AlgorithmProfile
        {
            Name = "표준",
            Algorithm = SelectedAlgorithm,
            Amounts = MartinSteps.Select(s => s.Amount).ToList()
        };

        var vm = new EditAlgorithmViewModel(SelectedAlgorithm, CloneProfile(source));
        var dlg = new EditAlgorithmWindow(vm)
        {
            Owner = Application.Current.MainWindow
        };

        if (dlg.ShowDialog() != true)
            return;

        var saved = vm.ToProfile();
        UpsertProfile(saved);
        _catalog.Save(_allProfiles);
        RefreshProfileOptions();
        SelectedProfile = ProfileOptions.FirstOrDefault(p =>
            p.Algorithm == saved.Algorithm && p.Name == saved.Name);
        AddLog($"알고리듬 프로필 저장: {SelectedAlgorithm} / {saved.Name}");
    }

    [RelayCommand]
    private void SimulateRound()
    {
        ApplySettingsToEngine();
        AddLog("시뮬레이션 1회 실행 (Ctrl+Shift+S)");

        CardModel open;
        CardModel hidden;

        if (_engine.Phase == EnginePhase.Active && _engine.ActiveSide is not null)
        {
            open = RandomCard();
            hidden = CardMatching(_engine.ActiveSide.Value, win: _rng.NextDouble() > 0.45);
        }
        else
        {
            var axis = IsUnderOverEnabled ? BetAxis.UnderOver : BetAxis.RedBlack;
            var streakSide = _rng.NextDouble() > 0.5
                ? (axis == BetAxis.UnderOver ? BetSide.Over : BetSide.Red)
                : (axis == BetAxis.UnderOver ? BetSide.Under : BetSide.Black);

            SeedSimulatedStreak(axis, streakSide, Math.Max(1, TargetStreakCount) - 1);
            open = CardMatching(streakSide, win: true);
            var betSide = streakSide.Opposite();
            hidden = CardMatching(betSide, win: _rng.NextDouble() > 0.4);
        }

        var shoeBefore = _engine.CurrentShoeRound;
        var isShoeStart = _engine.CurrentShoeRound == 1;
        var (decision, settle) = _engine.PlayRound(++_simRoundId, open, hidden);

        CurrentRoundId = _simRoundId;
        RemainingSeconds = 35;

        if (decision.Place)
        {
            NextBetText = decision.Side?.ToString() ?? "-";
            AddLog($"[시뮬] 베팅: {decision.Reason} / {decision.Side} {decision.Amount:N0}");
            TotalWagered += decision.Amount;
        }
        else
        {
            AddLog($"[시뮬] 스킵: {decision.Reason}");
        }

        AddLog($"[시뮬] 정산: {settle.Message}");
        ApplySettleToBalance(settle);
        RecordStepStats(settle);
        AppendResultDotFromLastHistory(isShoeStart);
        RefreshStatusFromEngine();
        UpdateMartinStepStatuses();
        _ = RefreshShoeOverlayAsync();

        if (shoeBefore >= 12 && _rng.NextDouble() > 0.5)
        {
            _engine.OnShuffleDetected();
            AddLog("[시뮬] 셔플 감지 → 1판");
            RefreshStatusFromEngine();
        }

    }

    private async Task PollOnceAsync()
    {
        if (!IsBotRunning || IsPaused)
            return;

        try
        {
            if (TargetProfit > 0 && Profit >= TargetProfit)
            {
                AddLog($"목표수익 도달 ({Profit:N0}). 자동 정지.");
                StopBot();
                return;
            }

            if (StopLoss > 0 && Profit <= -StopLoss)
            {
                AddLog($"손절 도달 ({Profit:N0}). 자동 정지.");
                StopBot();
                return;
            }

            ApplySettingsToEngine();
            var snap = await _bridge.PollGameStateAsync();
            if (snap is null)
                return;

            if (snap.TimerSeconds is int sec)
                RemainingSeconds = sec;

            if (snap.RoundId is int rid)
                CurrentRoundId = rid;

            if (snap.ShuffleDetected)
            {
                _engine.OnShuffleDetected();
                RefreshStatusFromEngine();
            }

            if (snap.TimerSeconds is >= 0 and <= 35
                && !string.IsNullOrWhiteSpace(snap.OpenCard)
                && !_awaitingHidden)
            {
                var openToken = snap.OpenCard.Trim();
                var roundKey = snap.RoundId ?? CurrentRoundId;

                if (_lastHandledRoundId == roundKey && _lastOpenToken == openToken)
                    return;

                if (!TryParseCard(openToken, out var openCard))
                {
                    AddLog($"오픈카드 파싱 실패: '{openToken}'");
                    return;
                }

                var decision = _engine.BeginRound(roundKey, openCard);
                _lastHandledRoundId = roundKey;
                _lastOpenToken = openToken;
                _awaitingHidden = true;

                if (decision.Place && decision.Side is not null)
                {
                    NextBetText = decision.Side.Value.ToString();
                    await _bridge.ClickBetAsync(decision.Side.Value, decision.Amount);
                    _engine.ConfirmBetPlaced();
                    TotalWagered += decision.Amount;
                    AddLog($"자동 베팅: {decision.Reason} / {decision.Side} {decision.Amount:N0}");
                }
                else
                {
                    NextBetText = "-";
                    AddLog($"관망: {decision.Reason}");
                }

                RefreshStatusFromEngine();
                UpdateMartinStepStatuses();
            }

            if (_awaitingHidden
                && snap.TimerSeconds is >= 50
                && !string.IsNullOrWhiteSpace(snap.HiddenCard))
            {
                if (!TryParseCard(snap.HiddenCard, out var hidden))
                {
                    AddLog($"히든카드 파싱 실패: '{snap.HiddenCard}'");
                    return;
                }

                var shoeStart = _engine.History.LastOrDefault()?.IsShoeStart == true
                                || _engine.CurrentShoeRound == 1;
                // Capture shoe-start flag from pending round before settle advances.
                var pendingIsStart = _engine.CurrentShoeRound == 1;

                var settle = _engine.SettleHidden(hidden, shuffleAfter: snap.ShuffleDetected);
                _awaitingHidden = false;
                AddLog($"정산: {settle.Message}");
                ApplySettleToBalance(settle);
                RecordStepStats(settle);
                AppendResultDotFromLastHistory(pendingIsStart);
                RefreshStatusFromEngine();
                UpdateMartinStepStatuses();
                await RefreshShoeOverlayAsync();
                _ = shoeStart;
            }
        }
        catch (Exception ex)
        {
            AddLog($"폴링 오류: {ex.Message}");
        }
    }

    private async Task RefreshShoeOverlayAsync()
    {
        try
        {
            await _bridge.ApplyShoeStartOverlaysAsync(_shoeStartBeadIndices);
        }
        catch
        {
            // Site DOM may not be ready / selector mismatch — ignore quietly.
        }
    }

    private void RefreshProfileOptions()
    {
        ProfileOptions.Clear();
        foreach (var p in _allProfiles.Where(p => p.Algorithm == SelectedAlgorithm)
                     .OrderBy(p => p.Name == "표준" ? 0 : 1)
                     .ThenBy(p => p.Name))
            ProfileOptions.Add(p);
    }

    private void LoadStepsFromSelectedProfile()
    {
        MartinSteps.Clear();
        var amounts = SelectedProfile?.Amounts;
        if (amounts is null || amounts.Count == 0)
            amounts = [1000, 2000, 4000, 8000, 16000, 32000, 64000, 128000];

        for (var i = 0; i < amounts.Count; i++)
            MartinSteps.Add(new MartingaleStep(i + 1, amounts[i]));
    }

    private void UpsertProfile(AlgorithmProfile saved)
    {
        var existing = _allProfiles.FirstOrDefault(p =>
            p.Algorithm == saved.Algorithm &&
            string.Equals(p.Name, saved.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            _allProfiles.Add(saved);
            return;
        }

        existing.Amounts = saved.Amounts;
        existing.IsBuiltIn = saved.Name == "표준";
    }

    private static AlgorithmProfile CloneProfile(AlgorithmProfile source) => new()
    {
        Name = source.Name,
        Algorithm = source.Algorithm,
        IsBuiltIn = source.IsBuiltIn,
        Amounts = source.Amounts.ToList()
    };

    private void ApplySettingsToEngine()
    {
        // Locked features remain always enabled.
        IsStreakSettingEnabled = true;
        IsStepFinishEnabled = true;

        var settings = new BettingSettings
        {
            IsUnderOverEnabled = IsUnderOverEnabled,
            IsRedBlackEnabled = IsRedBlackEnabled,
            TargetStreakCount = Math.Max(1, TargetStreakCount),
            IsStepFinishEnabled = true,
            StepFinishRound = StepFinishRound,
            MartinAmounts = MartinSteps.OrderBy(s => s.Step).Select(s => s.Amount).ToList()
        };
        _engine.UpdateSettings(settings);
    }

    private void RefreshStatusFromEngine()
    {
        CurrentShoeRound = _engine.CurrentShoeRound;
        CurrentStep = _engine.CurrentStep;
        CurrentShoeText = $"{CurrentShoeRound}판";
        CurrentStepText = $"{CurrentStep}단계";
        PhaseText = _engine.Phase == EnginePhase.Active ? "진행중" : "대기";
        ActiveSideText = _engine.ActiveSide?.ToString() ?? "-";
        if (_engine.ActiveSide is not null)
            NextBetText = _engine.ActiveSide.Value.ToString();
    }

    private void UpdateMartinStepStatuses()
    {
        foreach (var step in MartinSteps)
        {
            if (_engine.Phase == EnginePhase.Active && step.Step == _engine.CurrentStep)
                step.Status = "진행";
            else if (step.Step < _engine.CurrentStep && _engine.Phase == EnginePhase.Active)
                step.Status = "완료";
            else
                step.Status = "대기";
        }
    }

    private void RecordStepStats(SettleResult settle)
    {
        if (settle.Outcome == BetOutcome.Skip || settle.StepBefore < 1)
            return;

        var row = MartinSteps.FirstOrDefault(s => s.Step == settle.StepBefore);
        if (row is null)
            return;

        row.PlayCount++;
        if (!settle.Won)
            row.LossRecord++;
    }

    private void AppendResultDotFromLastHistory(bool isShoeStart)
    {
        var last = _engine.History.LastOrDefault();
        if (last is null)
            return;

        if (isShoeStart)
            _shoeStartBeadIndices.Add(_beadCount);

        _beadCount++;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var item = ResultDotItem.FromRound(last);
            item.IsShoeStart = isShoeStart;
            ResultDots.Add(item);
            while (ResultDots.Count > 80)
                ResultDots.RemoveAt(0);
        });
    }

    private void ApplySettleToBalance(SettleResult settle)
    {
        if (settle.Outcome == BetOutcome.Skip)
            return;

        if (settle.Won)
        {
            Balance += settle.Amount;
            Profit += settle.Amount;
        }
        else
        {
            Balance -= settle.Amount;
            Profit -= settle.Amount;
        }
    }

    private void SeedSimulatedStreak(BetAxis axis, BetSide side, int priorCardCount)
    {
        _ = axis;
        _engine.ClearHistory();
        var need = priorCardCount - (priorCardCount % 2);
        var rounds = new List<RoundResult>();
        var id = 1;
        for (var i = 0; i < need; i += 2)
        {
            rounds.Add(new RoundResult
            {
                RoundId = id++,
                OpenCard = CardMatching(side, true),
                HiddenCard = CardMatching(side, true),
                ShoeRound = Math.Max(1, _engine.CurrentShoeRound),
                IsShoeStart = false
            });
        }

        _engine.SeedHistory(rounds);
    }

    private CardModel RandomCard() =>
        new(_rng.Next(1, 11), (Suit)_rng.Next(0, 4));

    private CardModel CardMatching(BetSide side, bool win)
    {
        var target = win ? side : side.Opposite();
        return target switch
        {
            BetSide.Under => new CardModel(_rng.Next(1, 6), RandomSuit()),
            BetSide.Over => new CardModel(_rng.Next(6, 11), RandomSuit()),
            BetSide.Black => new CardModel(_rng.Next(1, 11), _rng.NextDouble() > 0.5 ? Suit.Spade : Suit.Club),
            BetSide.Red => new CardModel(_rng.Next(1, 11), _rng.NextDouble() > 0.5 ? Suit.Diamond : Suit.Heart),
            _ => RandomCard()
        };
    }

    private Suit RandomSuit() => (Suit)_rng.Next(0, 4);

    private static bool TryParseCard(string token, out CardModel card)
    {
        try
        {
            card = CardModel.Parse(token);
            return true;
        }
        catch
        {
            card = null!;
            return false;
        }
    }

    private static string EnsureUrl(string domain)
    {
        var d = domain.Trim();
        if (!d.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            d = "https://" + d;
        return d;
    }

    private void AddLog(string message)
    {
        var entry = new LogEntry
        {
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Level = "INFO",
            Message = message
        };
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Logs.Insert(0, entry);
            while (Logs.Count > 300)
                Logs.RemoveAt(Logs.Count - 1);
        });
    }

    partial void OnIsUnderOverEnabledChanged(bool value) => ApplySettingsToEngine();
    partial void OnIsRedBlackEnabledChanged(bool value) => ApplySettingsToEngine();
    partial void OnTargetStreakCountChanged(int value) => ApplySettingsToEngine();
    partial void OnStepFinishRoundChanged(int value) => ApplySettingsToEngine();
}
