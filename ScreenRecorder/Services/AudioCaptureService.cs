using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;

namespace ScreenRecorder.Services;

public class AudioCaptureService : IDisposable
{
    private WasapiCapture? _micCapture;
    private WasapiLoopbackCapture? _loopbackCapture;
    private WaveFileWriter? _writer;
    private string? _outputPath;
    private readonly object _writeLock = new();

    // Resampler targets
    private const int TargetSampleRate = 44100;
    private const int TargetChannels = 2;
    private const int TargetBitDepth = 16;

    public string? OutputFilePath => _outputPath;

    public void Start(bool recordMic, bool recordLoopback, string wavOutputPath)
    {
        _outputPath = wavOutputPath;
        var targetFormat = new WaveFormat(TargetSampleRate, TargetBitDepth, TargetChannels);
        _writer = new WaveFileWriter(wavOutputPath, targetFormat);

        if (recordLoopback)
        {
            _loopbackCapture = new WasapiLoopbackCapture();
            _loopbackCapture.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded == 0) return;
                var converted = ConvertToTarget(e.Buffer, e.BytesRecorded, _loopbackCapture.WaveFormat, targetFormat);
                lock (_writeLock)
                    _writer?.Write(converted, 0, converted.Length);
            };
            _loopbackCapture.StartRecording();
        }

        if (recordMic)
        {
            _micCapture = new WasapiCapture();
            _micCapture.DataAvailable += (s, e) =>
            {
                if (e.BytesRecorded == 0) return;
                var converted = ConvertToTarget(e.Buffer, e.BytesRecorded, _micCapture.WaveFormat, targetFormat);
                lock (_writeLock)
                    _writer?.Write(converted, 0, converted.Length);
            };
            _micCapture.StartRecording();
        }
    }

    public void Stop()
    {
        _micCapture?.StopRecording();
        _loopbackCapture?.StopRecording();
        Thread.Sleep(300); // let final data flush
        lock (_writeLock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
        _micCapture?.Dispose();
        _micCapture = null;
        _loopbackCapture?.Dispose();
        _loopbackCapture = null;
    }

    private static byte[] ConvertToTarget(byte[] buffer, int bytesRecorded, WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        using var ms = new MemoryStream(buffer, 0, bytesRecorded);
        using var reader = new RawSourceWaveStream(ms, sourceFormat);

        // Build conversion chain
        ISampleProvider sampleProvider = reader.ToSampleProvider();

        if (sourceFormat.Channels == 1 && targetFormat.Channels == 2)
            sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
        else if (sourceFormat.Channels > 2)
            sampleProvider = new MultiplexingSampleProvider(new[] { sampleProvider }, targetFormat.Channels);

        if (sourceFormat.SampleRate != targetFormat.SampleRate)
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, targetFormat.SampleRate);

        var pcmProvider = sampleProvider.ToWaveProvider16();
        using var outMs = new MemoryStream();
        byte[] tmp = new byte[4096];
        int read;
        while ((read = pcmProvider.Read(tmp, 0, tmp.Length)) > 0)
            outMs.Write(tmp, 0, read);
        return outMs.ToArray();
    }

    public void Dispose()
    {
        Stop();
    }
}
