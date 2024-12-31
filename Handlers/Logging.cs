using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamic_Lighting_Key_Indicator
{
    public static class Logging
    {
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

                System.IO.File.AppendAllText(logFilePath, crashLog);
            }
            catch (Exception ex)
            {
                // If logging fails, continue to close gracefully so do nothing
                Debug.WriteLine("Failed to write crash log: " + ex);
            }
        }
    }
}
