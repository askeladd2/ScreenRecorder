using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenRecorder.Services;

public class ScreenCaptureService
{
    private Thread? _captureThread;
    private volatile bool _running;
    private volatile bool _paused;
    private Rectangle _region;
    private int _fps;
    private Action<byte[]>? _onFrame;

    public void StartCapture(Rectangle region, int fps, Action<byte[]> onFrame)
    {
        _region = region;
        _fps = fps;
        _onFrame = onFrame;
        _running = true;
        _paused = false;

        _captureThread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Name = "ScreenCapture"
        };
        _captureThread.Start();
    }

    public void StopCapture()
    {
        _running = false;
        _captureThread?.Join(2000);
        _captureThread = null;
    }

    public void PauseCapture() => _paused = true;
    public void ResumeCapture() => _paused = false;

    private void CaptureLoop()
    {
        var interval = TimeSpan.FromMilliseconds(1000.0 / _fps);
        while (_running)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (!_paused)
            {
                try
                {
                    var frame = CaptureFrame();
                    if (frame != null)
                        _onFrame?.Invoke(frame);
                }
                catch { /* swallow capture errors */ }
            }
            sw.Stop();
            var remaining = interval - sw.Elapsed;
            if (remaining > TimeSpan.Zero)
                Thread.Sleep(remaining);
        }
    }

    private byte[]? CaptureFrame()
    {
        using var bmp = new Bitmap(_region.Width, _region.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(_region.Location, Point.Empty, _region.Size, CopyPixelOperation.SourceCopy);

        // Convert to BGR24
        using var bmp24 = new Bitmap(_region.Width, _region.Height, PixelFormat.Format24bppRgb);
        using var g2 = Graphics.FromImage(bmp24);
        g2.DrawImage(bmp, 0, 0);

        var bmpData = bmp24.LockBits(
            new Rectangle(0, 0, bmp24.Width, bmp24.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            int stride = Math.Abs(bmpData.Stride);
            int bytes = stride * bmp24.Height;
            byte[] result = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, result, 0, bytes);

            // If stride != width*3, need to strip padding
            int rowSize = bmp24.Width * 3;
            if (stride == rowSize)
                return result;

            // Strip row padding
            byte[] stripped = new byte[rowSize * bmp24.Height];
            for (int y = 0; y < bmp24.Height; y++)
                Array.Copy(result, y * stride, stripped, y * rowSize, rowSize);
            return stripped;
        }
        finally
        {
            bmp24.UnlockBits(bmpData);
        }
    }
}
