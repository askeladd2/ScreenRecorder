using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using ScreenRecorder.ViewModels;

namespace ScreenRecorder;

public partial class MainWindow : Window
{
    private NotifyIcon? _trayIcon;
    private bool _reallyClose = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "ScreenRecorder",
            Visible = true,
            Icon = SystemIcons.Application
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Exit", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _reallyClose = true;
        _trayIcon?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            Hide();
        base.OnStateChanged(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _trayIcon?.Dispose();
        }
        base.OnClosing(e);
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}
