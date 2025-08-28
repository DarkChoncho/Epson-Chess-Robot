using Serilog;
using System;
using System.Runtime.CompilerServices;

namespace Chess_Project
{
    /// <summary>
    /// Centralized logging helper for the Chess project built on Serilog.
    /// Exposes convenience methods (<c>Fatal</c>, <c>Error</c>, <c>Warning</c>,
    /// <c>Information</c>, <c>Debug</c>) that automatically capture caller context
    /// (class name, method name, and line number) and write to a logger enriched
    /// with <c>SourceContext</c> = <c>"Chess_Project</c>.
    /// </summary>
    /// <remarks>
    /// Each method accepts an optional <see cref="System.Exception"/> and formats the message as
    /// "<c>{Class}.{Method}() line {LineNumber}: {Message}</c>". Caller information is supplied via
    /// <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>,
    /// <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>, and
    /// <see cref="System.Runtime.CompilerServices.CallerLineNumberAttribute"/>.
    /// <para>
    /// Thread-safety: Serilog's <see cref="Serilog.ILogger"/> is thread-safe; these helpers may be
    /// called concurrently from multiple threads.
    /// </para>
    /// <para>✅ Updated on 8/25/2025</para>
    /// </remarks>
    public static class ChessLog
    {
        public static Serilog.ILogger Logger => Log.Logger.ForContext("SourceContext", "Chess_Project");

        /// <summary>
        /// Writes a <c>Fatal</c>-level log entry for an unrecoverable error that will
        /// cause the application/script to terminate. The entry includes the caller's
        /// class, method, and line number for easier diagnosis.
        /// </summary>
        /// <param name="message">Human-readable description of the fatal condition being logged.</param>
        /// <param name="ex">The exception that triggered the fatal condition, if available; otherwise, <c>null</c>.</param>
        /// <param name="methodName">The name of the calling member. Supplied automatically vie <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="filePath">The full source file path of the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="lineNumber">The source line number within the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerLineNumberAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <remarks>
        /// Formats the log as "<c>{Class}.{Method}() line {LineNumber}: {Message}</c>" and includes
        /// the exception details when provided. Use this method for non-recoverable failures;
        /// prefer an error-level logger for recoverable conditions.
        /// <para>✅ Updated on 8/25/2025</para>
        /// </remarks>
        public static void LogFatal(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Fatal(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        /// <summary>
        /// Writes an <c>Error</c>-level log entry for a handled or recoverable failure.
        /// The entry includes the caller's class, method, and line number for easier diagnosis.
        /// </summary>
        /// <param name="message">Human-readable description of the error condition being logged.</param>
        /// <param name="ex">The exception that triggered the error condition, if available; otherwise, <c>null</c>.</param>
        /// <param name="methodName">The name of the calling member. Supplied automatically vie <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="filePath">The full source file path of the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="lineNumber">The source line number within the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerLineNumberAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <remarks>
        /// Formats the log as "<c>{Class}.{Method}() line {LineNumber}: {Message}</c>" and includes
        /// the exception details when provided. Use this method for recoverable failures;
        /// prefer a warning-level logger for non-failure conditions.
        /// <para>✅ Updated on 8/25/2025</para>
        /// </remarks>
        public static void LogError(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Error(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        /// <summary>
        /// Writes a <c>Warning</c>-level log entry for an unexpected but recoverable condition.
        /// The entry includes the caller's class, method, and line number for easier diagnosis.
        /// </summary>
        /// <param name="message">Human-readable description of the warning condition being logged.</param>
        /// <param name="ex">The exception that triggered the warning condition, if available; otherwise, <c>null</c>.</param>
        /// <param name="methodName">The name of the calling member. Supplied automatically vie <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="filePath">The full source file path of the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="lineNumber">The source line number within the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerLineNumberAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <remarks>
        /// Formats the log as "<c>{Class}.{Method}() line {LineNumber}: {Message}</c>" and includes
        /// the exception details when provided. Use this method for degraded states or suspicious
        /// conditions where the application can continue. Prefer an information-level
        /// logger for non-failure conditions.
        /// <para>✅ Updated on 8/25/2025</para>
        /// </remarks>
        public static void LogWarning(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Warning(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        /// <summary>
        /// Writes an <c>Information</c>-level log entry for routine application events,
        /// state changes, or progress messages.
        /// The entry includes the caller's class, method, and line number for easier diagnosis.
        /// </summary>
        /// <param name="message">Human-readable description of the information being logged.</param>
        /// <param name="ex">The exception that triggered the information, if available; otherwise, <c>null</c>.</param>
        /// <param name="methodName">The name of the calling member. Supplied automatically vie <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="filePath">The full source file path of the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="lineNumber">The source line number within the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerLineNumberAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <remarks>
        /// Formats the log as "<c>{Class}.{Method}() line {LineNumber}: {Message}</c>" and includes
        /// the exception details when provided. For verbose diagnostic details, consider a debug-level
        /// logger.
        /// <para>✅ Updated on 8/25/2025</para>
        /// </remarks>
        public static void LogInformation(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Information(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }

        /// <summary>
        /// Writes a <c>Debug</c>-level log entry with verbose diagnostic details
        /// (e.g., internal state, timings, and control-flow breadcrumbs).
        /// The entry includes the caller's class, method, and line number for easier diagnosis.
        /// </summary>
        /// <param name="message">Human-readable description of the verbose information being logged.</param>
        /// <param name="ex">The exception that triggered the verbose information, if available; otherwise, <c>null</c>.</param>
        /// <param name="methodName">The name of the calling member. Supplied automatically vie <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="filePath">The full source file path of the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerFilePathAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <param name="lineNumber">The source line number within the caller. Supplied automatically via <see cref="System.Runtime.CompilerServices.CallerLineNumberAttribute"/>.
        /// Do not pass explicitly.</param>
        /// <remarks>
        /// Formats the log as "<c>{Class}.{Method}() line {LineNumber}: {Message}</c>" and includes
        /// the exception details when provided. Intended primarily for development
        /// and deep troubleshooting; consider reducing verbosity in production.
        /// <para>✅ Updated on 8/25/2025</para>
        /// </remarks>
        public static void LogDebug(string message, Exception? ex = null, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Logger.Debug(ex, $"{className}.{methodName}() line {lineNumber}: {message}");
        }
    }
}