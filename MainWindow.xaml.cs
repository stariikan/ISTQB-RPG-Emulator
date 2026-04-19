using CommunityToolkit.Mvvm.ComponentModel;
using ISTQBEmulator.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text;
using WinRT.Interop;

namespace ISTQBEmulator
{
    public sealed partial class MainWindow : Window
    {
        // 🔥 Set this to false to disable all desktop logging
        private const bool EnableLogging = false;
        private readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ISTQB_XAML_Log.txt");

        public MainViewModel ViewModel { get; set; }
        private AppWindow _appWindow;

        public MainWindow()
        {
            if (EnableLogging) File.WriteAllText(_logPath, "LOG: App Start\n");

            try
            {
                WriteToLog("LOG: Creating ViewModel...");
                ViewModel = new MainViewModel();

                WriteToLog("LOG: InitializeComponent Starting...");
                this.InitializeComponent();

                WriteToLog("LOG: UI Built Successfully.");
                AppRoot.DataContext = ViewModel;

                WriteToLog("LOG: Setting Window Icon...");

                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
                _appWindow = AppWindow.GetFromWindowId(wndId);
                _appWindow.Title = "ISTQB RPG Emulator";

                // Absolute path for the icon
                string iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");

                if (File.Exists(iconPath))
                {
                    _appWindow.SetIcon(iconPath);
                    WriteToLog("LOG: Icon set successfully.");
                }
                else
                {
                    WriteToLog($"LOG: WARNING - Icon file NOT FOUND at {iconPath}");
                }
            }
            catch (Exception ex)
            {
                if (EnableLogging)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("\n!!! FATAL XAML PARSE ERROR !!!");
                    Exception? currentEx = ex;
                    while (currentEx != null)
                    {
                        sb.AppendLine($"\n[Error Type]: {currentEx.GetType().Name}");
                        sb.AppendLine($"[Message]: {currentEx.Message}");
                        sb.AppendLine($"[Stack]: {currentEx.StackTrace}");
                        currentEx = currentEx.InnerException;
                    }
                    File.AppendAllText(_logPath, sb.ToString());
                }
                throw;
            }
        }

        private void WriteToLog(string message)
        {
            if (EnableLogging)
            {
                File.AppendAllText(_logPath, message + "\n");
            }
        }

        private void OnVariantClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Core.Models.AnswerVariant variant)
            {
                ViewModel.SubmitAnswer(variant.Label);
            }
        }
    }
}