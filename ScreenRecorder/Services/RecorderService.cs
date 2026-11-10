using System.Diagnostics;
using System.Drawing;
using System.IO;
using ScreenRecorder.Models;
using SharpAvi;
using SharpAvi.Output;

namespace ScreenRecorder.Services;

public class RecorderService
{
    private RecordingSettings? _settings;
    private ScreenCaptureService? _screenCapture;
    private AudioCaptureService? _audioCapture;
    private AviWriter? _aviWriter;
    private IAviVideoStream? _videoStream;
    private string? _tempAviPath;
    private string? _tempWavPath;
    private string? _tempDir;
    private Stopwatch _elapsed = new();
    private volatile bool _isRecording;
    private volatile bool _isPaused;

    public bool IsRecording => _isRecording;
    public bool IsPaused => _isPaused;
    public TimeSpan ElapsedTime => _elapsed.Elapsed;

    public async Task StartAsync(RecordingSettings settings)
    {
        _settings = settings;
        _isRecording = true;
        _isPaused = false;

        // Setup temp paths
        _tempDir = Path.Combine(Path.GetTempPath(), $"ScreenRec_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempAviPath = Path.Combine(_tempDir, "raw.avi");
        _tempWavPath = Path.Combine(_tempDir, "audio.wav");

        // Determine capture region
        Rectangle region = settings.Mode == RecordingMode.FullScreen
            ? GetPrimaryScreenBounds()
            : settings.CaptureRegion;

        // Ensure even dimensions for ffmpeg
        int width = region.Width % 2 == 0 ? region.Width : region.Width - 1;
        int height = region.Height % 2 == 0 ? region.Height : region.Height - 1;
        region = new Rectangle(region.X, region.Y, width, height);

        // Setup AVI writer
        _aviWriter = new AviWriter(_tempAviPath)
        {
            FramesPerSecond = settings.TargetFps,
            EmitIndex1 = true
        };
        _videoStream = _aviWriter.AddVideoStream(width, height);
        _videoStream.Codec = KnownFourCCs.Codecs.Uncompressed;
        _videoStream.BitsPerPixel = BitsPerPixel.Bpp24;

        // Start audio
        bool captureAudio = settings.RecordMicrophone || settings.RecordSystemAudio;
        if (captureAudio)
        {
            _audioCapture = new AudioCaptureService();
            _audioCapture.Start(settings.RecordMicrophone, settings.RecordSystemAudio, _tempWavPath);
        }

        // Start screen capture
        _screenCapture = new ScreenCaptureService();
        _screenCapture.StartCapture(region, settings.TargetFps, OnFrameCaptured);

        _elapsed.Start();
        await Task.CompletedTask;
    }

    private void OnFrameCaptured(byte[] frameData)
    {
        if (_videoStream == null || _isPaused) return;
        try
        {
            lock (_aviWriter!)
            {
                _videoStream.WriteFrame(true, frameData, 0, frameData.Length);
            }
        }
        catch { /* ignore frame write errors during stop */ }
    }

    public void Pause()
    {
        if (!_isRecording || _isPaused) return;
        _isPaused = true;
        _screenCapture?.PauseCapture();
        _elapsed.Stop();
    }

    public void Resume()
    {
        if (!_isRecording || !_isPaused) return;
        _isPaused = false;
        _screenCapture?.ResumeCapture();
        _elapsed.Start();
    }

    public async Task<string> StopAsync()
    {
        _isRecording = false;
        _isPaused = false;
        _elapsed.Stop();

        // Stop capture services
        _screenCapture?.StopCapture();
        _audioCapture?.Stop();

        // Finalize AVI
        lock (_aviWriter!)
        {
            _aviWriter.Close();
        }
        _aviWriter = null;
        _videoStream = null;

        // Generate output path
        string outputPath = _settings!.GenerateOutputPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // Mux with ffmpeg
        bool hasAudio = File.Exists(_tempWavPath) && new FileInfo(_tempWavPath!).Length > 44; // more than WAV header
        await MuxWithFfmpegAsync(_tempAviPath!, _tempWavPath!, outputPath, hasAudio, _settings.FfmpegPath);

        // Cleanup temp files
        try
        {
            if (_tempDir != null && Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { /* best effort */ }

        return outputPath;
    }

    private static async Task MuxWithFfmpegAsync(string aviPath, string wavPath, string outputPath, bool hasAudio, string ffmpegPath)
    {
        // Try to find ffmpeg
        string ffmpeg = ResolveFfmpegPath(ffmpegPath);

        string args;
        if (hasAudio && File.Exists(wavPath))
            args = $"-y -i \"{aviPath}\" -i \"{wavPath}\" -c:v libx264 -preset fast -crf 23 -c:a aac -shortest \"{outputPath}\"";
        else
            args = $"-y -i \"{aviPath}\" -c:v libx264 -preset fast -crf 23 \"{outputPath}\"";

        var psi = new ProcessStartInfo(ffmpeg, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) throw new InvalidOperationException("Failed to start ffmpeg. Make sure ffmpeg is installed and in PATH.");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            string err = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"ffmpeg failed (exit {proc.ExitCode}): {err}");
        }
    }

    private static string ResolveFfmpegPath(string configured)
    {
        // 1. User-configured path
        if (File.Exists(configured)) return configured;
        // 2. Relative to exe
        string exeDir = AppContext.BaseDirectory;
        string relative = Path.Combine(exeDir, configured);
        if (File.Exists(relative)) return relative;
        // 3. PATH
        return "ffmpeg";
    }

    private static Rectangle GetPrimaryScreenBounds()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        return screen != null
            ? new Rectangle(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height)
            : new Rectangle(0, 0, 1920, 1080);
    }
}
