/*
 * FILE             : Logger.cs
 * PROJECT          : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 * PROGRAMMER       : Julia Jakob
 * FIRST VERSION    : 2026-03-27
 * DESCRIPTION      : Logs workstation activity to unique files named WorkstationName_YYYY-MM-DD_HH-mm-ss.log
 */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P01_WorkstationSimulation
{
    internal class Logger
    {
        private const string DefaultLogDirectory = "Logs"; // Logs folder
        private const string DefaultLogFileName = "workstation.log"; // default log name
        private static string _logFileName = ""; // create a variable to store the new log file name in
        private static readonly object _logLock = new object(); // lock to control log access
        private static bool _initialized = false; // keep track of if the log has been initialized
        private static string _currentWorkstationName = ""; // create a variable to store te selected work station name

        /// <summary>
        /// Initializes logger for specific workstation with unique filename
        /// </summary>
        /// <param name="workstationName">name of selected work station</param>
        internal static void Initialize(string workstationName)
        {
            // keep file access controlled within the lock
            lock (_logLock)
            {
                // check to see if the log has been initialized yet
                if (!_initialized || _currentWorkstationName != workstationName)
                {
                    _currentWorkstationName = workstationName; // set workstation name
                    _logFileName = GenerateUniqueLogFileName(workstationName); // generate a unique file name
                    _initialized = true; // set initialized to true

                    // initialize the log with a title message
                    try
                    {
                        string directory = Path.GetDirectoryName(_logFileName);

                        // create the directory if it doesn't exist
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // write log header
                        File.WriteAllText(_logFileName, "===============================================\n");
                        File.AppendAllText(_logFileName, $"JUBI WORKSTATION LOG - {_currentWorkstationName}\n");
                        File.AppendAllText(_logFileName, "===============================================\n");
                        File.AppendAllText(_logFileName, $"Session: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                        File.AppendAllText(_logFileName, $"Workstation: {_currentWorkstationName}\n");
                        File.AppendAllText(_logFileName, "===============================================\n\n");
                    }
                    // catch file access errors
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Logger init error for {_currentWorkstationName}: {ex.Message}");
                        _logFileName = "";
                        _initialized = false;
                    }
                }
            }
        }

        /// <summary>
        /// Generates unique log filename: WorkstationName_YYYY-MM-DD_HH-mm-ss.log
        /// </summary>
        private static string GenerateUniqueLogFileName(string workstationName)
        {
            string safeName = workstationName.Replace(" ", "_").Replace("-", "_").Replace("/", "_"); // Clean workstation name for filename (remove invalid chars)
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string uniqueFileName = $"{safeName}_{timestamp}.log";

            return Path.Combine(DefaultLogDirectory, uniqueFileName);
        }

        /// <summary>
        /// Logs message to workstation's log file (SKIPS if no workstation is selected)
        /// </summary>
        internal static void Log(string message)
        {
            // Skip logging if no workstation selected/initialized
            if (!_initialized || string.IsNullOrEmpty(_logFileName))
            {
                return;  // Silently skip - no crash, no default
            }

            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";

            lock (_logLock)
            {
                try
                {
                    File.AppendAllText(_logFileName, logMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Log write error: {ex.Message}");
                }
            }
        }

       
    }
}





