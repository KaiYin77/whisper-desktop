using System.IO;
using System.Windows;
using OpenCCNET;

namespace WhisperApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var baseDir = AppContext.BaseDirectory;
        ZhConverter.Initialize(
            dictionaryDirectory: Path.Combine(baseDir, "Dictionary"),
            jiebaResourceDirectory: Path.Combine(baseDir, "JiebaResource"));
    }
}

