using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenCCNET;

namespace WhisperDesktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var baseDir = AppContext.BaseDirectory;
        ZhConverter.Initialize(
            dictionaryDirectory: Path.Combine(baseDir, "Dictionary"),
            jiebaResourceDirectory: Path.Combine(baseDir, "JiebaResource"));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
