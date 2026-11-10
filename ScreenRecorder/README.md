# ScreenRecorder

A full-featured screen recorder built with C# / WPF (.NET 8).

## Features
- **Full Screen or Region Recording** – capture the entire primary monitor or drag to select any area
- **Audio Capture** – microphone (WASAPI) + system audio (WASAPI loopback) simultaneously
- **Start / Stop / Pause / Resume** controls
- **MP4 output** via ffmpeg (H.264 + AAC)
- **System tray icon** – minimize to tray, quick exit
- **Settings** – FPS (15/30/60), microphone, system audio, output folder
- **Recording timer** display
- **Modern flat dark-theme WPF UI**

## Requirements

### Runtime
- Windows 10/11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **ffmpeg** – must be available on your `PATH`, or place `ffmpeg.exe` at `tools\ffmpeg.exe` relative to the exe

### Build
- .NET 8 SDK

## Setup

### 1. Install ffmpeg
Download from https://ffmpeg.org/download.html and either:
- Add it to your system `PATH`, **or**
- Copy `ffmpeg.exe` into `ScreenRecorder\tools\ffmpeg.exe`

### 2. Build
```powershell
cd C:\Users\RADHE\.gemini\antigravity\scratch\ScreenRecorder
dotnet build ScreenRecorder\ScreenRecorder.csproj
```

### 3. Run
```powershell
dotnet run --project ScreenRecorder\ScreenRecorder.csproj
```
Or run the built executable from `bin\Debug\net8.0-windows\`.

## Usage

| Button | Action |
|---|---|
| ● (red circle) | Start recording |
| ■ (gray square) | Stop recording & save |
| ⏸ | Pause / Resume |
| 🔲 Select Region | Draw a region overlay |
| 📁 Browse | Pick output folder |

Recordings are saved as `Recording_YYYYMMDD_HHmmss.mp4` in the selected output folder.

## Architecture

```
MainWindow (WPF)
  └── MainViewModel (MVVM)
        ├── RecorderService          (orchestrates)
        │     ├── ScreenCaptureService  (GDI screen grab → AVI frames)
        │     └── AudioCaptureService   (NAudio WASAPI → WAV)
        └── After stop: ffmpeg mux AVI+WAV → MP4
```

## Notes
- The AVI temp file uses **uncompressed BGR24** frames — it may be large on disk temporarily
- ffmpeg is called at the end of each recording to produce a compressed MP4
- System audio capture requires **Windows 10+** and uses WASAPI loopback
