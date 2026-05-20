using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WhisperApp;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".mp3", ".wav", ".m4a", ".aac", ".flac", ".ogg", ".wma"
    };

    private readonly Brush _dropZoneNormal = Brushes.White;
    private readonly Brush _dropZoneHover = new SolidColorBrush(Color.FromRgb(245, 245, 245));
    private CancellationTokenSource? _transcriptionCts;

    public ObservableCollection<TranscriptJob> Jobs { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void ChooseFiles_Click(object sender, RoutedEventArgs e) => ChooseFiles();

    private void DropZone_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => ChooseFiles();

    private void DropZone_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        DropZone.Background = _dropZoneHover;
    }

    private void DropZone_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        DropZone.Background = _dropZoneNormal;
    }

    private void ChooseFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select media files",
            Multiselect = true,
            Filter = "Media files|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.wma|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = GetUsableFiles(e).Any() ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Background = _dropZoneHover;
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.Background = _dropZoneNormal;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.Background = _dropZoneNormal;
        var files = GetUsableFiles(e).ToArray();
        if (files.Length == 0)
        {
            SetStatus("No supported files found.");
            AppendLog("Drop ignored: no supported files.");
            return;
        }

        AddFiles(files);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var pendingJobs = Jobs.Where(job => job.State is JobState.Pending or JobState.Failed).ToList();
        if (pendingJobs.Count == 0)
        {
            SetStatus("No files to transcribe.");
            AppendLog("Start ignored: no pending jobs.");
            return;
        }

        _transcriptionCts = new CancellationTokenSource();
        SetBusy(true);
        ClearLog();
        AppendLog($"Queued {pendingJobs.Count} file(s).");

        try
        {
            var completedBeforeRun = Jobs.Count(job => job.State == JobState.Completed);
            var totalForProgress = completedBeforeRun + pendingJobs.Count;
            var done = completedBeforeRun;
            UpdateProgress(done, totalForProgress);

            foreach (var job in pendingJobs)
            {
                _transcriptionCts.Token.ThrowIfCancellationRequested();

                job.State = JobState.Running;
                job.Status = "Running";
                SetStatus($"Transcribing {job.FileName}");
                AppendLog($"[{job.FileName}] starting");

                var request = BuildRequest(job.FilePath);
                var result = await RunWhisperAsync(request, _transcriptionCts.Token);

                if (result.ExitCode != 0)
                {
                    job.State = JobState.Failed;
                    job.Status = "Failed";
                    job.Error = result.Output.Trim();
                    AppendLog($"[{job.FileName}] failed with exit code {result.ExitCode}");
                    if (!string.IsNullOrWhiteSpace(result.Output))
                    {
                        AppendLog(result.Output.Trim());
                    }
                }
                else
                {
                    var transcript = LoadTranscript(request.OutputPath);
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        transcript = result.Output.Trim();
                    }

                    File.WriteAllText(request.OutputPath, transcript, Encoding.UTF8);
                    job.OutputPath = request.OutputPath;
                    job.State = JobState.Completed;
                    job.Status = "Done";
                    AppendLog($"[{job.FileName}] completed");
                    AppendLog($"[{job.FileName}] output: {request.OutputPath}");
                }

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
                job.State = JobState.Pending;
                job.Status = "Waiting";
            }

            SetStatus("Canceled.");
            AppendLog("Canceled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Transcription failed: {ex.Message}");
            AppendLog(ex.ToString());
        }
        finally
        {
            _transcriptionCts?.Dispose();
            _transcriptionCts = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _transcriptionCts?.Cancel();
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not TranscriptJob job || string.IsNullOrWhiteSpace(job.OutputPath) || !File.Exists(job.OutputPath))
        {
            SetStatus("Transcript file not found.");
            AppendLog("Download ignored: transcript file missing.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save transcript",
            FileName = Path.GetFileName(job.OutputPath),
            Filter = "Text file|*.txt|All files|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.Copy(job.OutputPath, dialog.FileName, overwrite: true);
            SetStatus($"Saved to {dialog.FileName}");
            AppendLog($"Downloaded: {dialog.FileName}");
        }
    }

    private void ClearCompleted_Click(object sender, RoutedEventArgs e)
    {
        for (var index = Jobs.Count - 1; index >= 0; index--)
        {
            if (Jobs[index].State == JobState.Completed)
            {
                Jobs.RemoveAt(index);
            }
        }

        UpdateProgress(Jobs.Count(job => job.State == JobState.Completed), Math.Max(Jobs.Count, 1));
        SetStatus(Jobs.Count == 0 ? "Waiting for files." : "Completed items cleared.");
    }

    private void AddFiles(IEnumerable<string> files)
    {
        var added = 0;
        var existing = Jobs.Select(job => job.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.Where(File.Exists))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(file)) || existing.Contains(file))
            {
                continue;
            }

            Jobs.Add(new TranscriptJob(file));
            existing.Add(file);
            added++;
        }

        SetStatus(added == 0 ? "No new files added." : $"Added {added} file(s).");
        UpdateProgress(Jobs.Count(job => job.State == JobState.Completed), Math.Max(Jobs.Count, 1));
    }

    private WhisperRequest BuildRequest(string mediaPath)
    {
        var model = ((ComboBoxItem)ModelCombo.SelectedItem).Content?.ToString() ?? "base";
        var languageTag = ((ComboBoxItem)LanguageCombo.SelectedItem).Tag?.ToString() ?? "";
        var outputDirectory = Path.GetDirectoryName(mediaPath) ?? Environment.CurrentDirectory;
        var outputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(mediaPath)}.逐字稿.txt");
        var command = ResolveCommand();

        var arguments = new List<string>(command.BaseArguments)
        {
            mediaPath,
            "--model", model,
            "--task", "transcribe",
            "--output_format", "txt",
            "--output_dir", outputDirectory
        };

        if (!string.IsNullOrWhiteSpace(languageTag))
        {
            arguments.Add("--language");
            arguments.Add(languageTag);
        }

        return new WhisperRequest(command.Executable, arguments, outputPath);
    }

    private static WhisperCommand ResolveCommand()
    {
        if (CommandExists("whisper.exe"))
        {
            return new WhisperCommand("whisper.exe", []);
        }

        if (CommandExists("whisper"))
        {
            return new WhisperCommand("whisper", []);
        }

        if (CommandExists("py"))
        {
            return new WhisperCommand("py", ["-3.12", "-m", "whisper"]);
        }

        if (CommandExists("python"))
        {
            return new WhisperCommand("python", ["-m", "whisper"]);
        }

        throw new InvalidOperationException("Whisper CLI not found. Install openai-whisper and ffmpeg first.");
    }

    private async Task<ProcessResult> RunWhisperAsync(WhisperRequest request, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Executable,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => AppendProcessLine(args.Data, output);
        process.ErrorDataReceived += (_, args) => AppendProcessLine(args.Data, output);

        AppendLog($"Launching: {request.Executable}");

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start Whisper process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, output.ToString());
    }

    private void AppendProcessLine(string? line, StringBuilder output)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        output.AppendLine(line);
        Dispatcher.Invoke(() => AppendLog(line));
    }

    private static string LoadTranscript(string expectedOutputPath)
    {
        if (File.Exists(expectedOutputPath))
        {
            return File.ReadAllText(expectedOutputPath, Encoding.UTF8);
        }

        var directory = Path.GetDirectoryName(expectedOutputPath);
        var fileName = Path.GetFileNameWithoutExtension(expectedOutputPath).Replace(".逐字稿", "", StringComparison.Ordinal);
        var whisperOutputPath = directory is null ? $"{fileName}.txt" : Path.Combine(directory, $"{fileName}.txt");

        if (!File.Exists(whisperOutputPath))
        {
            return "";
        }

        var transcript = File.ReadAllText(whisperOutputPath, Encoding.UTF8);
        File.Copy(whisperOutputPath, expectedOutputPath, overwrite: true);
        return transcript;
    }

    private void SetBusy(bool isBusy)
    {
        StartButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = isBusy;
        ModelCombo.IsEnabled = !isBusy;
        LanguageCombo.IsEnabled = !isBusy;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void AppendLog(string message)
    {
        LogText.AppendText(message + Environment.NewLine);
        LogText.ScrollToEnd();
    }

    private void ClearLog()
    {
        LogText.Clear();
    }

    private void UpdateProgress(int completed, int total)
    {
        ProgressBar.Value = total <= 0 ? 0 : completed * 100.0 / total;
    }

    private static IEnumerable<string> GetUsableFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return [];
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.Where(file => File.Exists(file) && SupportedExtensions.Contains(Path.GetExtension(file)));
    }

    private static bool CommandExists(string command)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        var candidates = Path.HasExtension(command)
            ? paths.Select(path => Path.Combine(path, command))
            : paths.SelectMany(path => new[] { Path.Combine(path, command), Path.Combine(path, $"{command}.exe") });

        return candidates.Any(File.Exists);
    }

    private sealed record WhisperCommand(string Executable, List<string> BaseArguments);
    private sealed record WhisperRequest(string Executable, List<string> Arguments, string OutputPath);
    private sealed record ProcessResult(int ExitCode, string Output);
}

public enum JobState
{
    Pending,
    Running,
    Completed,
    Failed
}

public sealed class TranscriptJob : INotifyPropertyChanged
{
    private string _status = "Waiting";
    private string? _outputPath;
    private string? _error;
    private JobState _state = JobState.Pending;

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
        set
        {
            if (SetField(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }

    public string? Error
    {
        get => _error;
        set => SetField(ref _error, value);
    }

    public JobState State
    {
        get => _state;
        set
        {
            if (SetField(ref _state, value))
            {
                OnPropertyChanged(nameof(CanDownload));
            }
        }
    }

    public bool CanDownload => State == JobState.Completed && !string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
