using System;
using System.Diagnostics;
using System.IO;

namespace Dynamic_Lighting_Key_Indicator
{
    public static class Logging
    {
        private static readonly string _CrashlogFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Dynamic_Lighting_Key_Indicator_Log.txt");
        private static readonly string _DebugLogFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DebugLog_Dynamic_Lighting_Key_Indicator.txt");
        private static readonly string timestamp = DateTime.Now.ToString("MM-dd_HH-mm-ss");

        private static StreamWriter? debugLogFileStream = null;

        public static bool DebugFileLoggingEnabled { get; private set; } = false;

        public static void WriteCrashLog(Exception? exception)
        {
            try
            {
                // Try writing a log to the temp directory for debugging
                string logFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Dynamic_Lighting_Key_Indicator_Crash_Log.txt");

                string crashLog = $"""
                    Timestamp: {DateTime.Now}
                    Message: {exception?.Message}
                    Exception: {exception}
                    Stack Trace: {exception?.StackTrace}
                    Source: {exception?.Source}
                    Target Site: {exception?.TargetSite}


                    """;

                System.IO.File.AppendAllText(_CrashlogFilePath, crashLog);
            }
            catch (Exception ex)
            {
                // If logging fails, continue to close gracefully so do nothing
                Debug.WriteLine("Failed to write crash log: " + ex);
            }
        }

        public static void WriteDebug(string message, string beforeTimstamp = "", string suffix = "")
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (DebugFileLoggingEnabled == true)
            {
                if (debugLogFileStream == null)
                {
                    Debug.WriteLine($"(Console) [{time}] {message}");
                    InitializeFileLogging(GetOrCreateFileStream(_DebugLogFilePath));

                    // If it's still null, something went wrong
                    if (debugLogFileStream == null)
                    {
                        Debug.WriteLine($"(Console) [{time}] {message}");
                        return;
                    }
                }

                try
                {
                    debugLogFileStream.WriteLine($"{beforeTimstamp}[{time}] {message}{suffix}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error Trying to write to debug log: " + ex.Message);
                }
            }
            // Using Debug.Writeline also writes to the file, so only do this if file logging is disabled
            else
            {
                Debug.WriteLine($"(Console) [{time}] {message}");
            }
        }

        private static void InitializeFileLogging(StreamWriter debugStream)
        {
            if (!DebugFileLoggingEnabled)
            {
                // Set up a debug log file
                DebugFileLoggingEnabled = true;
                WriteDebug("------------------------------- Debug logging Enabled -------------------------------", beforeTimstamp: "\n");
            }
        }

        private static StreamWriter GetOrCreateFileStream(string filePath)
        {
            if (debugLogFileStream != null)
            {
                return debugLogFileStream;
            }

            if (!File.Exists(filePath))
            {
                File.Create(filePath);
            }

            debugLogFileStream = new StreamWriter(path: filePath, append: true);
            debugLogFileStream.AutoFlush = true;

            return debugLogFileStream;
        }

        private static void CloseFileLogging()
        {
            if (DebugFileLoggingEnabled == true)
            {
                WriteDebug("------------------------------- Disabling debug logging -------------------------------");
                DebugFileLoggingEnabled = false;
            }

            if (debugLogFileStream != null)
            {
                debugLogFileStream.Close();
                debugLogFileStream = null;
            }
        }

        public static void EnableDebugLog()
        {
            StreamWriter debugLogStream = GetOrCreateFileStream(_DebugLogFilePath);
            InitializeFileLogging(debugLogStream);
        }

        public static void DisableDebugLog()
        {
            CloseFileLogging();
        }
    }
}
