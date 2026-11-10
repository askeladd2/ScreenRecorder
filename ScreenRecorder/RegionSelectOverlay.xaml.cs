using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfKey = System.Windows.Input.Key;

namespace ScreenRecorder;

public partial class RegionSelectOverlay : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDragging;

    public Rectangle SelectedRegion { get; private set; } = Rectangle.Empty;

    public RegionSelectOverlay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Center the instruction banner
        Canvas.SetLeft(InstructionBorder, (ActualWidth - 420) / 2);
        Canvas.SetTop(InstructionBorder, 40);
    }

    protected override void OnMouseMove(WpfMouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
            UpdateSelectionRect(e.GetPosition(OverlayCanvas));
    }

    private void Canvas_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isDragging = true;
        SelectionRect.Visibility = Visibility.Visible;
        OverlayCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_isDragging)
            UpdateSelectionRect(e.GetPosition(OverlayCanvas));
    }

    private void Canvas_MouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        OverlayCanvas.ReleaseMouseCapture();

        var endPoint = e.GetPosition(OverlayCanvas);
        int x = (int)Math.Min(_startPoint.X, endPoint.X);
        int y = (int)Math.Min(_startPoint.Y, endPoint.Y);
        int w = (int)Math.Abs(endPoint.X - _startPoint.X);
        int h = (int)Math.Abs(endPoint.Y - _startPoint.Y);

        if (w > 10 && h > 10)
        {
            // Convert WPF coordinates to screen pixels (DPI aware)
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            // Account for window position
            var screenPos = PointToScreen(new System.Windows.Point(x, y));
            SelectedRegion = new Rectangle(
                (int)(screenPos.X),
                (int)(screenPos.Y),
                (int)(w * dpiX),
                (int)(h * dpiY));
        }

        DialogResult = true;
        Close();
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == WpfKey.Escape)
        {
            SelectedRegion = Rectangle.Empty;
            DialogResult = false;
            Close();
        }
    }

    private void UpdateSelectionRect(System.Windows.Point current)
    {
        double x = Math.Min(_startPoint.X, current.X);
        double y = Math.Min(_startPoint.Y, current.Y);
        double w = Math.Abs(current.X - _startPoint.X);
        double h = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        // Update dimension label
        DimText.Text = $"{(int)w} × {(int)h}";
        Canvas.SetLeft(DimLabel, x + w / 2 - 30);
        Canvas.SetTop(DimLabel, y + h + 4);
        DimLabel.Visibility = Visibility.Visible;
    }
}
