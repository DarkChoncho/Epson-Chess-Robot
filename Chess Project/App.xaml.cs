using Serilog;
using System;
using System.IO;
using System.Windows;

namespace Chess_Project
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log Files",
                    $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}", "Log.txt"),
                    rollingInterval: RollingInterval.Infinite,
                    outputTemplate: "[{level:u3}] {Timestamp:HH:mm:ss MM:dd:yyy}\n{Message:1}{NewLine}{Exception}\n")
                .CreateLogger();
        }
    }
}