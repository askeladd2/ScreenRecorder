using System.Drawing;

namespace ScreenRecorder.Models;

public enum RecordingMode { FullScreen, Region }

public class RecordingSettings
{
    public RecordingMode Mode { get; set; } = RecordingMode.FullScreen;
    public Rectangle CaptureRegion { get; set; }
    public int TargetFps { get; set; } = 30;
    public int Quality { get; set; } = 23;
    public bool RecordMicrophone { get; set; } = true;
    public bool RecordSystemAudio { get; set; } = true;
    public string OutputFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    public string FfmpegPath { get; set; } = "tools\\ffmpeg.exe";
    public string GenerateOutputPath() =>
        System.IO.Path.Combine(OutputFolder, $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
}
