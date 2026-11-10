namespace ScreenRecorder;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        // Global exception handler
        DispatcherUnhandledException += (s, ex) =>
        {
            System.Windows.MessageBox.Show($"Unexpected error: {ex.Exception.Message}", "ScreenRecorder Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
