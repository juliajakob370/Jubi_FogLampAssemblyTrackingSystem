/*
 * FILE           : RunnerLogger.cs
 * PROJECT        : Project Manufacturing - RunnerDisplay
 * PROGRAMMERS    : Bibi Murwared
 * FIRST VERSION  : 2026-04-15
 * DESCRIPTION    : Logs runner activity to a text file so the refill cycle
 *                  and runner events can be tracked outside the UI.
 */

using System;
using System.IO;

namespace RunnerDisplay
{
    internal static class RunnerLogger
    {
        private const string DefaultLogDirectory = "Logs"; // folder where log files will be stored
        private static string _logFileName = string.Empty; // full log file path
        private static readonly object _logLock = new object(); // lock to keep writes safe
        private static bool _initialized = false; // track whether logger has been initialized

        /// <summary>
        /// Initializes the runner log file for the current session
        /// </summary>
        internal static void Initialize()
        {
            lock (_logLock)
            {
                if (_initialized)
                {
                    return;
                }

                try
                {
                    // create Logs folder if needed
                    if (!Directory.Exists(DefaultLogDirectory))
                    {
                        Directory.CreateDirectory(DefaultLogDirectory);
                    }

                    // create unique file name using timestamp
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    _logFileName = Path.Combine(DefaultLogDirectory, $"RunnerDisplay_{timestamp}.log");

                    // write log header
                    File.WriteAllText(_logFileName, "===============================================\n");
                    File.AppendAllText(_logFileName, "JUBI RUNNER DISPLAY LOG\n");
                    File.AppendAllText(_logFileName, "===============================================\n");
                    File.AppendAllText(_logFileName, $"Session: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                    File.AppendAllText(_logFileName, "===============================================\n\n");

                    _initialized = true;
                }
                catch
                {
                    _initialized = false;
                    _logFileName = string.Empty;
                }
            }
        }

        /// <summary>
        /// Writes a timestamped message to the runner log file
        /// </summary>
        /// <param name="message">message to write into the log</param>
        internal static void Log(string message)
        {
            if (!_initialized || string.IsNullOrEmpty(_logFileName))
            {
                return;
            }

            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";

            lock (_logLock)
            {
                try
                {
                    File.AppendAllText(_logFileName, logMessage + Environment.NewLine);
                }
                catch
                {
                    // do nothing if logging fails
                }
            }
        }
    }
}