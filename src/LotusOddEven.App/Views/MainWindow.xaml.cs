using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LotusOddEven.App.Config;
using LotusOddEven.App.Services;
using LotusOddEven.App.ViewModels;

namespace LotusOddEven.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var bridge = new WebViewBridgeService(new DefaultGameDomSelectors());
        var vm = new MainViewModel(bridge);
        DataContext = vm;
        vm.AttachWebView(GameWebView);

        UpdateMaximizeRestoreIcon();

        Loaded += async (_, _) =>
        {
            try
            {
                await bridge.EnsureCoreAsync();
                if (!string.IsNullOrWhiteSpace(vm.AddressUrl))
                    await bridge.NavigateAsync(vm.AddressUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"WebView2 초기화 실패:\n{ex.Message}\n\nEvergreen WebView2 Runtime이 설치돼 있는지 확인하세요.",
                    "Lotus Bot",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // WindowStyle=None 최대화 시 작업표시줄까지 덮지 않도록 제한
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmGetMinMaxInfo = 0x0024;
        if (msg == wmGetMinMaxInfo)
        {
            RestrictMaximizedSizeToWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void RestrictMaximizedSizeToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        const uint monitorDefaultToNearest = 2;
        var monitor = MonitorFromWindow(hwnd, monitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return;

        var monitorInfo = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return;

        var work = monitorInfo.Work;
        var monitorArea = monitorInfo.Monitor;

        // 최대화 위치/크기를 해당 모니터의 작업 영역(태스크바 제외)으로 맞춤
        mmi.MaxPosition.X = Math.Abs(work.Left - monitorArea.Left);
        mmi.MaxPosition.Y = Math.Abs(work.Top - monitorArea.Top);
        mmi.MaxSize.X = Math.Abs(work.Right - work.Left);
        mmi.MaxSize.Y = Math.Abs(work.Bottom - work.Top);

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e) => UpdateMaximizeRestoreIcon();

    private void UpdateMaximizeRestoreIcon()
    {
        var maximized = WindowState == WindowState.Maximized;
        if (MaximizeIcon is not null)
            MaximizeIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        if (RestoreIcon is not null)
            RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;
        if (MaximizeRestoreButton is not null)
            MaximizeRestoreButton.ToolTip = maximized ? "이전 크기로 복원" : "최대화";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        if (WindowState == WindowState.Maximized)
            return;

        try
        {
            DragMove();
        }
        catch
        {
            // ignore during state transitions
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SitePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SitePassword = SitePasswordBox.Password;
    }

    #region Win32 maximize work-area

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    #endregion
}
