using System.Windows;
using OpenCCNET;

namespace WhisperApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ZhConverter.Initialize();
    }
}

