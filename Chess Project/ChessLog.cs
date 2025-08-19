using Serilog;
using System;
using System.Runtime.CompilerServices;

namespace Chess_Project
{
    public static class ChessLog
    {
        public static Serilog.ILogger Logger => Log.Logger.ForContext("SourceContext", "Chess_Project");

        public static void LogFatal(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Fatal(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        public static void LogError(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Error(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        public static void LogWarning(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Warning(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        public static void LogInformation(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Information(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        public static void LogDebug(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Debug(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }
    }
}
