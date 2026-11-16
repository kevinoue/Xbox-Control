using System;
using System.IO;

namespace XboxControl.Addin
{
    /// <summary>Small append-only log at %LOCALAPPDATA%\XboxControl\addin.log. Never throws.</summary>
    internal static class Log
    {
        private const long MaxBytes = 1_000_000;
        private static readonly object Gate = new();

        public static string FilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxControl", "addin.log");

        public static void Info(string message) => Write("INFO ", message);
        public static void Error(string context, Exception ex) => Write("ERROR", $"{context}: {ex}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > MaxBytes) info.Delete();
                    File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never take SolidWorks down.
            }
        }
    }
}
