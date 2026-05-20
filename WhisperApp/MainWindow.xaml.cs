using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
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

    private readonly Brush _dropZoneNormal = new SolidColorBrush(Color.FromRgb(247, 250, 255));
    private readonly Brush _dropZoneActive = new SolidColorBrush(Color.FromRgb(232, 242, 255));
    private CancellationTokenSource? _transcriptionCts;
    private string? _selectedFilePath;
    private string? _lastOutputPath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "選擇影片或音訊檔",
            Filter = "Media files|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.wma|All files|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            SelectFile(dialog.FileName);
        }
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = HasUsableFile(e) ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Background = _dropZoneActive;
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.Background = _dropZoneNormal;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.Background = _dropZoneNormal;
        if (!HasUsableFile(e))
        {
            SetStatus("請拖放支援的影片或音訊檔。");
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        SelectFile(files[0]);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFilePath is null)
        {
            SetStatus("請先選擇檔案。");
            return;
        }

        if (!File.Exists(_selectedFilePath))
        {
            SetStatus("找不到選擇的檔案。");
            return;
        }

        _transcriptionCts = new CancellationTokenSource();
        SetBusy(true);
        OutputText.Clear();
        _lastOutputPath = null;
        OpenOutputButton.IsEnabled = false;

        try
        {
            var request = BuildRequest(_selectedFilePath);
            AppendOutput($"Command: {request.DisplayCommand}{Environment.NewLine}{Environment.NewLine}");
            SetStatus($"使用 {request.Model} 模型轉錄中...");

            var result = await RunWhisperAsync(request, _transcriptionCts.Token);

            if (result.ExitCode != 0)
            {
                SetStatus("轉錄失敗。請確認 Whisper CLI、ffmpeg 與模型都已安裝。");
                AppendOutput($"{Environment.NewLine}Exit code: {result.ExitCode}{Environment.NewLine}");
                return;
            }

            var transcript = LoadTranscript(request.OutputPath);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                transcript = result.Output.Trim();
            }

            File.WriteAllText(request.OutputPath, transcript, Encoding.UTF8);
            OutputText.Text = transcript;
            _lastOutputPath = request.OutputPath;
            OpenOutputButton.IsEnabled = true;
            SetStatus($"完成：{request.OutputPath}");
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消。");
        }
        catch (Exception ex)
        {
            SetStatus("轉錄失敗。");
            AppendOutput(ex.Message);
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

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OutputText.Text))
        {
            Clipboard.SetText(OutputText.Text);
            SetStatus("已複製輸出內容。");
        }
    }

    private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutputPath is null || !File.Exists(_lastOutputPath))
        {
            SetStatus("尚無可開啟的逐字稿。");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastOutputPath,
            UseShellExecute = true
        });
    }

    private void SelectFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
        {
            SetStatus($"不支援的副檔名：{extension}");
            return;
        }

        _selectedFilePath = filePath;
        SelectedFileText.Text = filePath;
        SetStatus("已選擇檔案。");
    }

    private WhisperRequest BuildRequest(string mediaPath)
    {
        var model = ((ComboBoxItem)ModelCombo.SelectedItem).Content?.ToString() ?? "base";
        var languageTag = ((ComboBoxItem)LanguageCombo.SelectedItem).Tag?.ToString() ?? "";
        var outputDirectory = Path.GetDirectoryName(mediaPath) ?? Environment.CurrentDirectory;
        var outputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(mediaPath)}.逐字稿.txt");
        var commandText = CommandText.Text.Trim();

        var command = ResolveCommand(commandText);
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

        return new WhisperRequest(
            command.Executable,
            arguments,
            model,
            outputPath,
            $"{command.Executable} {string.Join(' ', arguments.Select(QuoteForDisplay))}");
    }

    private static WhisperCommand ResolveCommand(string commandText)
    {
        if (!string.IsNullOrWhiteSpace(commandText) && !commandText.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var parts = SplitCommandLine(commandText);
            if (parts.Count == 0)
            {
                throw new InvalidOperationException("Whisper 指令不可為空。");
            }

            return new WhisperCommand(parts[0], parts.Skip(1).ToList());
        }

        if (CommandExists("whisper.exe"))
        {
            return new WhisperCommand("whisper.exe", []);
        }

        if (CommandExists("whisper"))
        {
            return new WhisperCommand("whisper", []);
        }

        if (CommandExists("python"))
        {
            return new WhisperCommand("python", ["-m", "whisper"]);
        }

        if (CommandExists("py"))
        {
            return new WhisperCommand("py", ["-m", "whisper"]);
        }

        throw new InvalidOperationException("找不到 Whisper。請安裝 openai-whisper，或在 Whisper 指令欄填入完整指令。");
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

        if (!process.Start())
        {
            throw new InvalidOperationException("無法啟動 Whisper 行程。");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, output.ToString());
    }

    private void AppendProcessLine(string? line, StringBuilder output)
    {
        if (line is null)
        {
            return;
        }

        output.AppendLine(line);
        Dispatcher.Invoke(() =>
        {
            OutputText.AppendText(line + Environment.NewLine);
            OutputText.ScrollToEnd();
        });
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
        ProgressBar.IsIndeterminate = isBusy;
        ModelCombo.IsEnabled = !isBusy;
        LanguageCombo.IsEnabled = !isBusy;
        CommandText.IsEnabled = !isBusy;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void AppendOutput(string text)
    {
        OutputText.AppendText(text);
        OutputText.ScrollToEnd();
    }

    private static bool HasUsableFile(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return false;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.Length > 0 && File.Exists(files[0]) && SupportedExtensions.Contains(Path.GetExtension(files[0]));
    }

    private static bool CommandExists(string command)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        var candidates = Path.HasExtension(command)
            ? paths.Select(path => Path.Combine(path, command))
            : paths.SelectMany(path => new[] { Path.Combine(path, command), Path.Combine(path, $"{command}.exe") });

        return candidates.Any(File.Exists);
    }

    private static string QuoteForDisplay(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static List<string> SplitCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    private sealed record WhisperCommand(string Executable, List<string> BaseArguments);
    private sealed record WhisperRequest(string Executable, List<string> Arguments, string Model, string OutputPath, string DisplayCommand);
    private sealed record ProcessResult(int ExitCode, string Output);
}
