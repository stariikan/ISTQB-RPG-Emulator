using ISTQBEmulator.Data;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text;

namespace ISTQBEmulator
{
    public partial class App : Application
    {
        private Window _window;

        public App()
        {
            this.InitializeComponent();

            // Catch any fatal errors before the app closes
            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            StringBuilder report = new StringBuilder();
            report.AppendLine("=== ISTQB EMULATOR CRASH REPORT ===");
            report.AppendLine($"Date: {DateTime.Now}");
            report.AppendLine($"Message: {e.Message}");
            report.AppendLine("-----------------------------------");

            // Drill down into Inner Exceptions to find the REAL XAML error
            Exception ex = e.Exception;
            int depth = 0;
            while (ex != null)
            {
                report.AppendLine($"[Level {depth}] {ex.GetType().Name}: {ex.Message}");
                report.AppendLine($"Stack Trace: {ex.StackTrace}");
                report.AppendLine("-----------------------------------");
                ex = ex.InnerException;
                depth++;
            }

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFile = Path.Combine(desktop, "ISTQB_Detailed_Crash.txt");
            File.WriteAllText(logFile, report.ToString());

            // Attempt to show a native message box so the user knows it failed
            // (Optional: requires P/Invoke or standard WinForms/WPF reference)
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // 🔥 STEP 1: Wait for the database to finish building and seeding completely!
                using (var db = new AppDbContext())
                {
                    await DataSeeder.InitializeAsync(db);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("\n=== STARTUP ERROR ===");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                System.Diagnostics.Debug.WriteLine("=====================\n");
            }

            // 🔥 STEP 2: NOW it is safe to open the window and let the ViewModel read the database!
            _window = new MainWindow();
            _window.Activate();
        }
    }
}