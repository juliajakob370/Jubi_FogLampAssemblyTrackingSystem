/*
 * FILE           : MainWindow.xaml.cs
 *  PROJECT       : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob
 *  FIRST VERSION : 2026-03-27
 *  DESCRIPTION   : The backend for the Workstation Simulation program that will update the database 
 *                  to simulate work flow
 *  REFERENCES    : https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer?view=windowsdesktop-10.0
 *                  https://wpf-tutorial.com/misc/dispatchertimer/
 *                  https://learn.microsoft.com/en-us/dotnet/api/system.data.common.dbconnection.openasync?view=net-10.0
 *                  https://learn.microsoft.com/en-us/dotnet/api/system.data.common.dbcommand.executescalarasync?view=net-10.0
*/
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;


// Program flow - pseudo code so i can better do this ✅
// - don't need to worry about restocking or defect rates yet - just timing ✅
// User opens workstation simulator ✅
// Load in the available Workers from the Workers Table ✅
// Load in the available Workstations from the Workstations table ✅
// User selects one Worker and one Workstation from the dropdowns ✅
// When they have selected options from both drop downs - start button is enabled ✅
// When the user clicks the start button it starts the simulation updating according to the config table values
// Dispatch Timer - use to update UI and to perform the DB updates on their workstation's parts
// Keep going until one bin reaches 0 for now
// should work for multiple instances
// exclude ones that are in use
// log all the stuff so we can check to make sure it's working




namespace P01_WorkstationSimulation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string connectionString; // create variable to store connection string from app.config
        private readonly Dictionary<string, string> configValues = new(); // create a dictionary to store config values
        private readonly int configRefreshTime = 5; // declare to avoid magic nums 
        private DispatcherTimer configRefreshTimer; // dispatch timer for the cofig refresh - so that the config tool can actually update the values being used

        // UI stuff
        private TimeSpan simulationTimeSpan = TimeSpan.Zero; // accumulated time
        private DispatcherTimer stopwatchTimer;              // timer for UI update
        private int simulationCycleCount = 0; // track completed cycles for scaled time

        // SIMULATION FIELDS 
        private bool isSimulating = false; // check to see if the program running a simulation
        private int selectedStationID; // selected station ID from UI
        private int selectedWorkerID; // selected worker ID from UI
       // private Dictionary<int, string> workerSkillNames = new();  // create a dictionary to map worker ID's to the name of their skill levels
        private DispatcherTimer simulationTimer; // timer for simulation
        private Workstation? selectedWorkstation; // store selected work station as a Workstation object
        private Worker? selectedWorker; // store selected worker as a worker object

        /// <summary>
        /// Main initialization
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["JubiConnection"].ConnectionString; // get the connection string from the app.config
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Load all of the data asynchronously so the main window can load
        /// </summary>
        /// <param name="sender">object that called the function</param>
        /// <param name="e">event info</param>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        /// <summary>
        /// Load the Workers from the worker table, the workstations from the workstations table and the configurations from the config file
        /// </summary>
        /// <returns></returns>
        private async Task LoadAllDataAsync()
        {
            await LoadWorkersAsync();
            await LoadWorkstationsAsync();
            await LoadConfigurationAsync();
        }

        /// <summary>
        /// Load the Workers from the Workers table asynchronously and put them in the combo box
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task LoadWorkersAsync()
        {
            var workers = new List<Worker>(); // make a new list of Workers

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // access the db asynchronously

                string query = @" SELECT w.WorkerID, w.WorkerName, w.SkillLevelID, s.SkillLevelName, s.Speed, s.DefectRate, w.StationID FROM Worker w
                                      INNER JOIN Skills s ON w.SkillLevelID = s.SkillLevelID
                                      ORDER BY w.WorkerName;";
                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    // read in workers create worker objects and add them to the list of workers
                    while (await reader.ReadAsync())
                    {
                        workers.Add(new Worker
                        {
                            WorkerID = reader.GetInt32(reader.GetOrdinal("WorkerID")),
                            WorkerName = reader.GetString(reader.GetOrdinal("WorkerName")),
                            SkillLevelID = reader.GetInt32(reader.GetOrdinal("SkillLevelID")),
                            SkillLevelName = reader.GetString(reader.GetOrdinal("SkillLevelName")),
                            Speed = reader.GetDecimal(reader.GetOrdinal("Speed")),
                            DefectRate = reader.GetDecimal(reader.GetOrdinal("DefectRate")),
                            StationID = reader.GetInt32(reader.GetOrdinal("StationID"))
                        });
                    }
                }
            }

            // load the Workers into the combo box in the UI
            WorkerSelect.ItemsSource = workers;
            WorkerSelect.DisplayMemberPath = "WorkerName";
            WorkerSelect.SelectedValuePath = "WorkerID";
        }

        /// <summary>
        /// Load the Workstations from the Workstations table asynchronously and put them in the combo box
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task LoadWorkstationsAsync()
        {
            var workstations = new List<Workstation>(); // make a new list of Workstations

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // access the db asynchronously

                string query = @"SELECT StationID, StationName FROM WorkStation ORDER BY StationName;"; // make the query

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    // read in workstations create workstation objects and add them to the list of workstations
                    while (await reader.ReadAsync())
                    {
                        workstations.Add(new Workstation
                        {
                            StationID = reader.GetInt32(0),
                            StationName = reader.GetString(1)
                        });
                    }
                }
            }

            // load the Workstations into the combo box in the UI
            WorkstationSelect.ItemsSource = workstations;
            WorkstationSelect.DisplayMemberPath = "StationName";
            WorkstationSelect.SelectedValuePath = "StationID";
        }

        /// <summary>
        /// Load config values on a timer 
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task LoadConfigurationAsync()
        {
            // Load initial values
            await RefreshConfigFromDatabaseAsync();

            // Start refresh timer (refreshes every fixed amount of seconds while the simulation is not happening)
            configRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(configRefreshTime)
            };

            configRefreshTimer.Tick += async (s, e) =>
            {
                if (!isSimulating)  // Only refresh when idle!
                {
                    await RefreshConfigFromDatabaseAsync();
                }
            };

            configRefreshTimer.Start();
        }

        /// <summary>
        /// Initializes and starts a timer that periodically refreshes the configuration from the database.
        /// The timer triggers a configuration refresh every specified amount of seconds unless simulation mode is
        /// active. If a timer is already running, it is stopped and replaced. This method should be called to ensure
        /// configuration updates are applied regularly during normal operation.
        /// </summary>
        private void StartConfigRefreshTimer()
        {
            configRefreshTimer?.Stop();
            configRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            configRefreshTimer.Tick += async (s, e) =>
            {
                if (!isSimulating) await RefreshConfigFromDatabaseAsync();
            };
            configRefreshTimer.Start();
        }

        /// <summary>
        /// Refresh config values so the simulation can still get the most up to date config values
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task RefreshConfigFromDatabaseAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string query = "SELECT ConfigName, ConfigValue FROM Configuration;"; // create the query

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    configValues.Clear(); // clear the existing values

                    // add the new config values
                    while (await reader.ReadAsync())
                    {
                        configValues[reader.GetString(0)] = reader.GetString(1);
                    }
                }
            }
        }


        /// <summary>
        /// Gets config value as int with default 0
        /// </summary>
        /// <param name="key">key to look for in the dictionary of config values</param>
        /// <returns>parsed int or 0</returns>
        private int GetConfigInt(string key)
        {
            if (configValues.ContainsKey(key))
            {
                return int.Parse(configValues[key]);
            }
            else
            {
                return 0;
            }
        }


        /// <summary>
        /// Gets config value as decimal with default 0
        /// </summary>
        /// <param name="key">key to look up in the dictionary of config values</param>
        /// <returns>parsed decimal or 0</returns>
        private decimal GetConfigDecimal(string configName)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["JubiConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = "SELECT ConfigValue FROM Configuration WHERE ConfigName = @ConfigName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ConfigName", configName);
                    object result = cmd.ExecuteScalar();

                    if (result == null)
                    {
                        throw new Exception($"Configuration value '{configName}' not found.");
                    }

                    return Convert.ToDecimal(result);
                }
            }
        }

        /// <summary>
        /// Get each of the worker's skill level names and put them in a disctionary for easier access later
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
       /* private async Task LoadWorkerSkillsAsync()
        {
            workerSkillNames.Clear(); // clear the dictionary to start fresh

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // connect to db asynchronously

                // create query
                string query = @"SELECT w.WorkerID, sl.SkillLevelName FROM Worker w JOIN Skills sl ON w.SkillLevelID = sl.SkillLevelID WHERE w.WorkerID = @workerID;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@workerID", selectedWorkerID);
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int workerID = reader.GetInt32(0);
                            string skillName = reader.GetString(1);
                            workerSkillNames[workerID] = skillName;
                        }
                    }
                }
            }

            Logger.Log($"Loaded skill for Worker {selectedWorkerID}: {workerSkillNames[selectedWorkerID]}");
        }
       */

        /// <summary>
        /// Used to check if the start button should be enabled - user needs to select values first
        /// </summary>
        private void CheckStartButton()
        {
            StartButton.IsEnabled =
                WorkstationSelect.SelectedValue != null && WorkerSelect.SelectedValue != null;
        }


        /// <summary>
        /// Handles the Click event of the Start button to initialize and begin the simulation process.
        /// </summary>
        /// <remarks>This method initializes logging, loads the selected worker's skills, and starts the
        /// simulation loop. It also updates the user interface to reflect the simulation state and disables controls to
        /// prevent changes during simulation.</remarks>
        /// <param name="sender">The source of the event, typically the Start button.</param>
        /// <param name="e">The event data associated with the Click event.</param>
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            selectedWorkstation = WorkstationSelect.SelectedItem as Workstation;
            selectedWorker = WorkerSelect.SelectedItem as Worker;

            if (selectedWorkstation == null || selectedWorker == null)
            {
                MessageBox.Show("Please select a worker and a workstation before starting.");
                return;
            }

            selectedStationID = selectedWorkstation.StationID;
            selectedWorkerID = selectedWorker.WorkerID;

            Logger.Initialize(selectedWorkstation.StationName);

            // await LoadWorkerSkillsAsync();

            Logger.Log($"Worker selected - {selectedWorker.WorkerName} ({selectedWorker.SkillLevelName}) | Speed: {selectedWorker.Speed} | DefectRate: {selectedWorker.DefectRate}");
            isSimulating = true;
            configRefreshTimer?.Stop();

            Logger.Log($"Simulation STARTED - Worker: {selectedWorker.WorkerName}, Station: {selectedWorkstation.StationName}");

            StartSimulationLoop();

            StopButton.IsEnabled = true;
            StartButton.IsEnabled = false;
            WorkerSelect.IsEnabled = false;
            WorkstationSelect.IsEnabled = false;
        }
        /// <summary>
        /// Main simulation loop with timescale
        /// </summary>
        /// <summary>
        /// Main simulation loop with timescale
        /// </summary>
        private void StartSimulationLoop()
        {
            if (selectedWorker == null)
            {
                Logger.Log("ERROR: No worker selected.");
                return;
            }

            decimal timescale = GetConfigDecimal("SimulationTimeScale");
            decimal workerAssemblyTime = CalculateAssemblyTime(selectedWorker);

            if (timescale <= 0)
            {
                timescale = 1;
            }

            decimal cycleInterval = workerAssemblyTime / timescale;

            simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds((double)cycleInterval)
            };
            simulationTimer.Tick += SimulationTimer_Tick;
            simulationTimer.Start();

            stopwatchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            stopwatchTimer.Tick += StopwatchTimer_Tick;
            stopwatchTimer.Start();

            simulationTimeSpan = TimeSpan.Zero;
            simulationCycleCount = 0;

            Logger.Log($"Simulation STARTED | Timescale: {timescale:F1}x | Worker Cycle Time: {workerAssemblyTime:F1}s | Timer Interval: {cycleInterval:F1}s");
        }


        /// <summary>
        /// Updates the stopwatch display every 100ms during simulation
        /// </summary>
        private void StopwatchTimer_Tick(object sender, EventArgs e)
        {
            simulationTimeSpan += stopwatchTimer.Interval; // accumulate real elapsed time

            // format as MM:ss for UI
            string formattedTime = $"{(int)simulationTimeSpan.TotalMinutes:D2}:{simulationTimeSpan.Seconds:D2}";
            StopwatchDisplay.Text = formattedTime;
        }

        /// <summary>
        /// Simulation tick - check bins → build lamp → repeat
        /// </summary>
        private async void SimulationTimer_Tick(object sender, EventArgs e)
        {
            // Check if all bins have parts left
            if (!await CheckBinsHavePartsAsync())
            {
                Logger.Log($"🛑 BIN EMPTY - Simulation Stopped!"); // will be changed later for final version to work with runners until goal is reached
                StopSimulation(); // stops for now ---- NEEDS TO BE UPDATED LATER FOR FINAL SUBMISSION TO JUST FLAG IT AND WAIT FOR RUNNER !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                return;
            }

            // Build lamp (consumes parts)
            await RunLampCycleAsync();
        }

        /// <summary>
        /// Check if all bins at this station have parts > 0
        /// </summary>
        /// <returns>bool false if there are any empty bins and true if the bins still all have parts</returns>
        private async Task<bool> CheckBinsHavePartsAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // access db asynchronously
                string query = @"SELECT COUNT(*) FROM Bin WHERE StationID = @stationID AND CurrentCount < 1"; // build query

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@stationID", selectedStationID); // add parameter with value to SQL wuery

                    object result = await cmd.ExecuteScalarAsync();
                    int emptyBins = result != null ? Convert.ToInt32(result) : 0; // get the count of the empty bins or set it to 0 if null
                    bool hasParts = emptyBins == 0; // hasParts is true as long as there are no empty bins

                    // if it doesn't have parts log it
                    if (!hasParts)
                    {
                        Logger.Log($"⚠️ {emptyBins} empty bin(s) at Station {selectedStationID}");

                    }

                    return hasParts; // return the bool
                }
            }
        }

        /// <summary>
        /// Executes the lamp cycle process for the specified product barcode asynchronously, recording assembly data
        /// and logging the operation result.
        /// </summary>
        /// <param name="barcode">The unique barcode identifying the product for which the lamp cycle is to be run. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
private async Task RunLampCycleAsync()
{
    Worker? selectedWorker = WorkerSelect.SelectedItem as Worker;
    Workstation? selectedStation = WorkstationSelect.SelectedItem as Workstation;

    if (selectedWorker == null || selectedStation == null)
    {
        MessageBox.Show("Please select a worker and workstation.");
        return;
    }

    decimal assemblyTime = CalculateAssemblyTime(selectedWorker);
    bool isDefective = CalculateDefectResult(selectedWorker);
    string barcode = GenerateBarcode(selectedStation.StationID);

    try
    {
        Logger.Log($"Starting lamp cycle | Barcode: {barcode} | Station: {selectedStation.StationID} | Worker: {selectedWorker.WorkerName}");

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            using (SqlCommand cmd = new SqlCommand("sp_RunLampCycle", connection))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Barcode", barcode);
                cmd.Parameters.AddWithValue("@StationID", selectedStation.StationID);
                cmd.Parameters.AddWithValue("@WorkerID", selectedWorker.WorkerID);
                cmd.Parameters.AddWithValue("@TrayID", 1);
                cmd.Parameters.AddWithValue("@AssemblyTime", assemblyTime);
                cmd.Parameters.AddWithValue("@IsDefective", isDefective ? 1 : 0);
                cmd.Parameters.AddWithValue("@Notes", isDefective ? "Fail" : "Pass");

                await cmd.ExecuteNonQueryAsync();
            }
        }

        Logger.Log($"Lamp cycle complete | {barcode} | {assemblyTime:F1}s | {(isDefective ? "Fail" : "Pass")}");
    }
    catch (Exception ex)
    {
        Logger.Log($"ERROR: {ex.Message}");
    }
}
        private string GenerateBarcode(int stationId)
        {
            return $"LAMP-S{stationId}-{DateTime.Now:yyyyMMddHHmmssfff}";
        }
        //------------------------------------------------------------------------------------------------------------------------
        /// <summary>
        /// Calculates the assembly time for the selected worker based on the base time
        /// from the configuration table and the worker's speed multiplier.
        /// </summary>
        /// <param name="selectedWorker">The worker currently assigned to the simulation.</param>
        /// <returns>The calculated assembly time in seconds.</returns>
        private decimal CalculateAssemblyTime(Worker selectedWorker)
        {
            decimal baseTime = GetConfigDecimal("BaseTime");
            return baseTime * selectedWorker.Speed;
        }

        /// <summary>
        /// Determines whether the selected worker produces a defective product
        /// by comparing a random roll against the worker's defect rate.
        /// </summary>
        /// <param name="selectedWorker">The worker currently assigned to the simulation.</param>
        /// <returns>
        /// True if the product is defective; otherwise, false.
        /// </returns>
        private bool CalculateDefectResult(Worker selectedWorker)
        {
            Random random = new Random();
            double roll = random.NextDouble() * 100.0;
            return roll < (double)selectedWorker.DefectRate;
        }
        /// <summary>
        /// Gracefully stop simulation + resume config refresh
        /// </summary>
        /// 
        private void StopSimulation()
        {
            simulationTimer?.Stop();
            stopwatchTimer?.Stop();
            isSimulating = false;
            StartConfigRefreshTimer();

            Dispatcher.Invoke(() =>
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                WorkerSelect.IsEnabled = true;
                WorkstationSelect.IsEnabled = true;
            });

            Logger.Log("Simulation STOPPED!");
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopSimulation();
        }

        private void WorkstationSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckStartButton();
        }

        private void WorkerSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CheckStartButton();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}