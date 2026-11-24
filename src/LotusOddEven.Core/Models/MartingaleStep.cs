using CommunityToolkit.Mvvm.ComponentModel;

namespace LotusOddEven.Core.Models;

/// <summary>
/// Editable martingale row bound to the UI DataGrid.
/// Columns: 단계 / 금액 / 패기록 / 횟수
/// </summary>
public partial class MartingaleStep : ObservableObject
{
    [ObservableProperty]
    private int _step;

    [ObservableProperty]
    private int _amount;

    /// <summary>Losses recorded at this step (패기록).</summary>
    [ObservableProperty]
    private int _lossRecord;

    /// <summary>Times this step was played (횟수).</summary>
    [ObservableProperty]
    private int _playCount;

    /// <summary>Internal status for highlighting (대기/진행/완료).</summary>
    [ObservableProperty]
    private string _status = "대기";

    public MartingaleStep()
    {
    }

    public MartingaleStep(int step, int amount, string status = "대기")
    {
        Step = step;
        Amount = amount;
        Status = status;
    }
}
