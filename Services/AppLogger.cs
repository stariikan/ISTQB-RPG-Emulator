using System;
using System.IO;

namespace ISTQBEmulator.Services
{
    public static class AppLogger
    {
        public static void Log(string message)
        {
            try
            {
                // This targets the exact same folder as your database and JSON save file
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ISTQBEmulator");
                Directory.CreateDirectory(folder);

                string logFile = Path.Combine(folder, "sync_log.txt");

                // Write the timestamp and the message
                string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // If the logger itself fails, fail silently so we don't crash the app
            }
        }
    }
}