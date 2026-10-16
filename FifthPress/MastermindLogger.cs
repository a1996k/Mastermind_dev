using Amazon.SimpleEmailV2.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Http.ExceptionHandling;

namespace FifthPress
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public static class MastermindLogger
    {
        private static readonly object _globalLock = new object();

        private static string _globalLogFilePath = "log.txt";
        private static readonly AsyncLocal<string> _currentScope = new AsyncLocal<string>();
        private static readonly ConcurrentDictionary<string, string> _scopedPaths = new ConcurrentDictionary<string, string>();

        public static void SetLogFilePath(string path)
        {
            _globalLogFilePath = path;
        }

        public static void SetScopedLog(string scopeName, string filePath)
        {
            _scopedPaths[scopeName] = filePath;
        }

        public static void UseScope(string scopeName)
        {
            _currentScope.Value = scopeName;
        }

        public static void ClearScope()
        {
            _currentScope.Value = null;
        }

        public static void Log(LogLevel level, string message)
        {
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            WriteLog(logMessage);
        }

        public static void LogException(Exception e)
        {
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [Error] {e}";
            WriteLog(logMessage);
        }

        private static void WriteLog(string message)
        {
            string pathToUse = _globalLogFilePath;

            var scope = _currentScope.Value;
            if (!string.IsNullOrEmpty(scope) && _scopedPaths.TryGetValue(scope, out var scopedPath))
            {
                pathToUse = scopedPath;
            }

            lock (_globalLock)
            {
                using (StreamWriter writer = new StreamWriter(pathToUse, true))
                {
                    writer.WriteLine(message);
                }
            }
        }
    }
}