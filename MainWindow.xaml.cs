using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OpenCCNET;
using Whisper.net;
using Whisper.net.Ggml;

#if WINDOWS
using NAudio.Wave;
#else
using FFMpegCore;
using FFMpegCore.Pipes;
#endif

namespace WhisperDesktop;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma"
    };

    private static string ModelsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhisperDesktop", "models");

    private readonly IBrush _dropZoneNormal = Brushes.Transparent;
    private readonly IBrush _dropZoneHover  = new SolidColorBrush(Color.FromRgb(245, 245, 245));
    private CancellationTokenSource? _transcriptionCts;

    public ObservableCollection<TranscriptJob> Jobs { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        DropZone.AddHandler(DragDrop.DropEvent,      DropZone_Drop);
        DropZone.AddHandler(DragDrop.DragOverEvent,  DropZone_DragOver);
        DropZone.AddHandler(DragDrop.DragLeaveEvent, DropZone_DragLeave);
    }

    private void DropZone_PointerEntered(object? sender, PointerEventArgs e)
        => DropZone.Background = _dropZoneHover;

    private void DropZone_PointerExited(object? sender, PointerEventArgs e)
        => DropZone.Background = _dropZoneNormal;

    private void DropZone_PointerReleased(object? sender, PointerReleasedEventArgs e)
        => _ = ChooseFilesAsync();

    private void DropZone_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Background = _dropZoneHover;
        e.Handled = true;
    }

    private void DropZone_DragLeave(object? sender, RoutedEventArgs e)
        => DropZone.Background = _dropZoneNormal;

    private void DropZone_Drop(object? sender, DragEventArgs e)
    {
        DropZone.Background = _dropZoneNormal;
        var files = GetUsableFiles(e).ToArray();
        if (files.Length == 0)
        {
            SetStatus("No supported files found.");
            return;
        }
        AddFiles(files);
    }

    private async Task ChooseFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select media files",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Media files")
                {
                    Patterns = ["*.mp4", "*.mov", "*.mkv", "*.avi", "*.webm",
                                "*.mp3", "*.wav", "*.m4a", "*.aac", "*.flac", "*.ogg", "*.wma"]
                }
            ]
        });

        if (files.Count > 0)
            AddFiles(files.Select(f => f.Path.LocalPath));
    }

    private async void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        var pendingJobs = Jobs.Where(job => job.State is JobState.Pending or JobState.Failed).ToList();
        if (pendingJobs.Count == 0)
        {
            SetStatus("No files to transcribe.");
            return;
        }

        _transcriptionCts = new CancellationTokenSource();
        SetBusy(true);
        ClearLog();
        AppendLog($"Queued {pendingJobs.Count} file(s).");

        try
        {
            var modelName   = ((ComboBoxItem)ModelCombo.SelectedItem!).Content?.ToString() ?? "base";
            var languageTag = ((ComboBoxItem)LanguageCombo.SelectedItem!).Tag?.ToString() ?? "";

            var modelPath = await EnsureModelAsync(modelName, _transcriptionCts.Token);

            var completedBeforeRun = Jobs.Count(job => job.State == JobState.Completed);
            var totalForProgress   = completedBeforeRun + pendingJobs.Count;
            var done               = completedBeforeRun;
            UpdateProgress(done, totalForProgress);

            foreach (var job in pendingJobs)
            {
                _transcriptionCts.Token.ThrowIfCancellationRequested();
                await TranscribeJobAsync(job, modelPath, languageTag, _transcriptionCts.Token);
                done++;
                UpdateProgress(done, totalForProgress);
            }

            SetStatus("All files completed.");
            AppendLog("All files completed.");
        }
        catch (OperationCanceledException)
        {
            foreach (var job in Jobs.Where(job => job.State == JobState.Running))
            {
                job.State  = JobState.Pending;
                job.Status = "Waiting";
            }
            SetStatus("Canceled.");
            AppendLog("Canceled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
            AppendLog(ex.ToString());
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
        => _transcriptionCts?.Cancel();

    private void OpenTxtButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as Button)?.Tag is not TranscriptJob job
                || string.IsNullOrWhiteSpace(job.OutputPath)
                || !File.Exists(job.OutputPath))
            {
                SetStatus("Transcript file not found.");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = job.OutputPath,
                UseShellExecute = true
            });
            SetStatus($"Opened {Path.GetFileName(job.OutputPath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Open failed: {ex.Message}");
        }
    }

    private void ClearCompleted_Click(object? sender, RoutedEventArgs e)
    {
        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            if (Jobs[i].State == JobState.Completed)
                Jobs.RemoveAt(i);
        }

        UpdateProgress(Jobs.Count(j => j.State == JobState.Completed), Math.Max(Jobs.Count, 1));
        SetStatus(Jobs.Count == 0 ? "Waiting for files." : "Completed items cleared.");
    }

    // ── Core transcription pipeline ──────────────────────────────────────────

    private async Task TranscribeJobAsync(TranscriptJob job, string modelPath, string language, CancellationToken ct)
    {
        job.State  = JobState.Running;
        job.Status = "Running";
        SetStatus($"Transcribing {job.FileName}");
        AppendLog($"[{job.FileName}] starting");

        try
        {
            SetStatus($"Decoding audio — {job.FileName}");
            var (wavStream, audioDuration) = await DecodeToWavAsync(job.FilePath);
            using var _ = wavStream;

            ct.ThrowIfCancellationRequested();

            SetStatus($"Transcribing — {job.FileName}");
            var transcript = await TranscribeAsync(modelPath, wavStream, audioDuration, language, ct);

            var outputDir  = Path.GetDirectoryName(job.FilePath) ?? Environment.CurrentDirectory;
            var outputPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(job.FilePath)}-逐字稿.txt");
            await File.WriteAllTextAsync(outputPath, transcript, Encoding.UTF8, ct);

            job.OutputPath = outputPath;
            job.State      = JobState.Completed;
            job.Status     = "Done";
            AppendLog($"[{job.FileName}] → {outputPath}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            job.State  = JobState.Failed;
            job.Status = "Failed";
            job.Error  = ex.Message;
            AppendLog($"[{job.FileName}] failed: {ex.Message}");
        }
    }

    private async Task<string> EnsureModelAsync(string modelName, CancellationToken ct)
    {
        Directory.CreateDirectory(ModelsDir);
        var modelPath = Path.Combine(ModelsDir, $"ggml-{modelName}.bin");

        if (File.Exists(modelPath))
        {
            AppendLog($"Model '{modelName}' loaded from cache.");
            return modelPath;
        }

        AppendLog($"Downloading model '{modelName}' — first run only, please wait...");
        SetStatus($"Downloading model '{modelName}'...");

        await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(MapGgmlType(modelName));
        await using var fileStream  = File.Create(modelPath);
        await modelStream.CopyToAsync(fileStream, ct);

        AppendLog($"Model '{modelName}' downloaded and cached.");
        return modelPath;
    }

    // Decode any audio/video file to 16 kHz mono WAV in memory.
    // Windows uses Windows Media Foundation via NAudio; other platforms use ffmpeg via FFMpegCore.
    private static Task<(MemoryStream wav, TimeSpan duration)> DecodeToWavAsync(string inputPath)
    {
#if WINDOWS
#pragma warning disable CA1416  // DecodeToWavWindows is only compiled on Windows via #if WINDOWS
        return Task.Run(() => DecodeToWavWindows(inputPath));
#pragma warning restore CA1416
#else
        return DecodeToWavFfmpegAsync(inputPath);
#endif
    }

#if WINDOWS
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static (MemoryStream wav, TimeSpan duration) DecodeToWavWindows(string inputPath)
    {
        using var reader    = new MediaFoundationReader(inputPath);
        var duration        = reader.TotalTime;
        var targetFormat    = new WaveFormat(16000, 16, 1);
        using var resampler = new MediaFoundationResampler(reader, targetFormat) { ResamplerQuality = 60 };

        var ms = new MemoryStream();
        WaveFileWriter.WriteWavFileToStream(ms, resampler);
        ms.Position = 0;
        return (ms, duration);
    }
#else
    private static async Task<(MemoryStream wav, TimeSpan duration)> DecodeToWavFfmpegAsync(string inputPath)
    {
        var info     = await FFProbe.AnalyseAsync(inputPath);
        var duration = info.Duration;

        var ms = new MemoryStream();
        await FFMpegArguments
            .FromFileInput(inputPath)
            .OutputToPipe(new StreamPipeSink(ms), options => options
                .ForceFormat("wav")
                .WithAudioSamplingRate(16000)
                .WithCustomArgument("-ac 1 -acodec pcm_s16le"))
            .ProcessAsynchronously();
        ms.Position = 0;
        return (ms, duration);
    }
#endif

    private async Task<string> TranscribeAsync(
        string modelPath, Stream wavStream, TimeSpan duration, string language, CancellationToken ct)
    {
        using var factory = WhisperFactory.FromPath(modelPath);
        var builder = factory.CreateBuilder();

        if (!string.IsNullOrEmpty(language))
            builder.WithLanguage(language);

        using var processor = builder.Build();

        var sb = new StringBuilder();
        await foreach (var segment in processor.ProcessAsync(wavStream, ct))
        {
            var ts = segment.Start.ToString(@"hh\:mm\:ss");
            sb.AppendLine($"[{ts}] {segment.Text.Trim()}");

            if (duration > TimeSpan.Zero)
            {
                var pct = segment.End.TotalSeconds / duration.TotalSeconds * 100.0;
                Dispatcher.UIThread.Post(() => ProgressBar.Value = Math.Clamp(pct, 0, 100));
            }
        }

        return ToTraditionalChinese(sb.ToString().Trim());
    }

    private static string ToTraditionalChinese(string text)
        => ZhConverter.HansToTW(text);

    private static GgmlType MapGgmlType(string model) => model switch
    {
        "tiny"     => GgmlType.Tiny,
        "base"     => GgmlType.Base,
        "small"    => GgmlType.Small,
        "medium"   => GgmlType.Medium,
        "large"    => GgmlType.LargeV1,
        "large-v2" => GgmlType.LargeV2,
        "large-v3" => GgmlType.LargeV3,
        _          => GgmlType.Base
    };

    // ── UI helpers ───────────────────────────────────────────────────────────

    private void AddFiles(IEnumerable<string> files)
    {
        var added    = 0;
        var existing = Jobs.Select(j => j.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.Where(File.Exists))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(file)) || existing.Contains(file))
                continue;

            Jobs.Add(new TranscriptJob(file));
            existing.Add(file);
            added++;
        }

        SetStatus(added == 0 ? "No new files added." : $"Added {added} file(s).");
        UpdateProgress(Jobs.Count(j => j.State == JobState.Completed), Math.Max(Jobs.Count, 1));
    }

    private void SetBusy(bool isBusy)
    {
        StartButton.IsEnabled  = !isBusy;
        CancelButton.IsEnabled = isBusy;
        ModelCombo.IsEnabled   = !isBusy;
        LanguageCombo.IsEnabled = !isBusy;
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void AppendLog(string message)
    {
        var text = (LogText.Text ?? "") + message + Environment.NewLine;
        LogText.Text = text;
        LogText.CaretIndex = text.Length;
    }

    private void ClearLog() => LogText.Text = "";

    private void UpdateProgress(int completed, int total)
        => ProgressBar.Value = total <= 0 ? 0 : completed * 100.0 / total;

    private static IEnumerable<string> GetUsableFiles(DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return [];

        return e.Data.GetFiles()
            ?.OfType<IStorageFile>()
            .Select(f => f.Path.LocalPath)
            .Where(path => File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
            ?? [];
    }
}

public enum JobState { Pending, Running, Completed, Failed }

public sealed class TranscriptJob : INotifyPropertyChanged
{
    private string   _status    = "Waiting";
    private string?  _outputPath;
    private string?  _error;
    private JobState _state     = JobState.Pending;

    public TranscriptJob(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    public string FilePath { get; }
    public string FileName { get; }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string? OutputPath
    {
        get => _outputPath;
        set { if (SetField(ref _outputPath, value)) OnPropertyChanged(nameof(CanDownload)); }
    }

    public string? Error
    {
        get => _error;
        set => SetField(ref _error, value);
    }

    public JobState State
    {
        get => _state;
        set { if (SetField(ref _state, value)) OnPropertyChanged(nameof(CanDownload)); }
    }

    public bool CanDownload =>
        State == JobState.Completed && !string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
