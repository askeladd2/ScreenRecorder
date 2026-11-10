using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using System.Drawing;
using ScreenRecorder.Models;
using ScreenRecorder.Services;

namespace ScreenRecorder.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly RecorderService _recorder = new();
    private readonly DispatcherTimer _timer;
    private RecordingSettings _settings = new();

    // --- State ---
    private bool _isRecording;
    private bool _isPaused;
    private string _elapsedTimeText = "00:00:00";
    private string _statusText = "Ready";
    private string _outputFolder;
    private int _selectedFps = 30;
    private bool _recordMic = true;
    private bool _recordSystemAudio = true;
    private string _recordingModeText = "Full Screen";
    private string _lastSavedPath = string.Empty;

    public ObservableCollection<int> FpsOptions { get; } = new() { 15, 30, 60 };

    public bool IsRecording
    {
        get => _isRecording;
        set { _isRecording = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotRecording)); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsNotRecording => !_isRecording;

    public bool IsPaused
    {
        get => _isPaused;
        set { _isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(PauseResumeLabel)); CommandManager.InvalidateRequerySuggested(); }
    }

    public string PauseResumeLabel => IsPaused ? "▶ Resume" : "⏸ Pause";

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        set { _elapsedTimeText = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set { _outputFolder = value; _settings.OutputFolder = value; OnPropertyChanged(); }
    }

    public int SelectedFps
    {
        get => _selectedFps;
        set { _selectedFps = value; _settings.TargetFps = value; OnPropertyChanged(); }
    }

    public bool RecordMic
    {
        get => _recordMic;
        set { _recordMic = value; _settings.RecordMicrophone = value; OnPropertyChanged(); }
    }

    public bool RecordSystemAudio
    {
        get => _recordSystemAudio;
        set { _recordSystemAudio = value; _settings.RecordSystemAudio = value; OnPropertyChanged(); }
    }

    public string RecordingModeText
    {
        get => _recordingModeText;
        set { _recordingModeText = value; OnPropertyChanged(); }
    }

    public string LastSavedPath
    {
        get => _lastSavedPath;
        set { _lastSavedPath = value; OnPropertyChanged(); }
    }

    // --- Commands ---
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand SelectRegionCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }

    public MainViewModel()
    {
        _outputFolder = _settings.OutputFolder;

        StartRecordingCommand = new RelayCommand(async _ => await StartRecording(), _ => !IsRecording);
        StopRecordingCommand = new RelayCommand(async _ => await StopRecording(), _ => IsRecording);
        PauseResumeCommand = new RelayCommand(_ => TogglePause(), _ => IsRecording);
        SelectRegionCommand = new RelayCommand(_ => SelectRegion(), _ => !IsRecording);
        BrowseOutputFolderCommand = new RelayCommand(_ => BrowseOutputFolder());
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            if (_recorder.IsRecording && !_recorder.IsPaused)
            {
                var t = _recorder.ElapsedTime;
                ElapsedTimeText = $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            }
        };
    }

    private async Task StartRecording()
    {
        try
        {
            StatusText = "Starting...";
            await _recorder.StartAsync(_settings);
            IsRecording = true;
            IsPaused = false;
            ElapsedTimeText = "00:00:00";
            _timer.Start();
            StatusText = $"Recording ({RecordingModeText}) @ {SelectedFps} FPS";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private async Task StopRecording()
    {
        try
        {
            StatusText = "Stopping...";
            _timer.Stop();
            IsRecording = false;
            IsPaused = false;
            string path = await _recorder.StopAsync();
            LastSavedPath = path;
            StatusText = $"Saved: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void TogglePause()
    {
        if (_recorder.IsPaused)
        {
            _recorder.Resume();
            IsPaused = false;
            StatusText = $"Recording ({RecordingModeText}) @ {SelectedFps} FPS";
        }
        else
        {
            _recorder.Pause();
            IsPaused = true;
            StatusText = "Paused";
        }
    }

    private void SelectRegion()
    {
        var overlay = new ScreenRecorder.RegionSelectOverlay();
        overlay.ShowDialog();
        if (overlay.SelectedRegion != Rectangle.Empty)
        {
            _settings.Mode = RecordingMode.Region;
            _settings.CaptureRegion = overlay.SelectedRegion;
            RecordingModeText = $"Region ({overlay.SelectedRegion.Width}x{overlay.SelectedRegion.Height})";
        }
        else
        {
            _settings.Mode = RecordingMode.FullScreen;
            RecordingModeText = "Full Screen";
        }
    }

    private void BrowseOutputFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Output Folder",
            SelectedPath = OutputFolder
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            OutputFolder = dialog.SelectedPath;
    }

    private void OpenOutputFolder()
    {
        if (System.IO.Directory.Exists(OutputFolder))
            System.Diagnostics.Process.Start("explorer.exe", OutputFolder);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
