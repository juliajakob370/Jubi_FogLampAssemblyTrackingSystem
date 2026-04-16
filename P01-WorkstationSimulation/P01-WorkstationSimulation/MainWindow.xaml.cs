/*
 * FILE           : MainWindow.xaml.cs
 *  PROJECT       : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob & Bibi Murwared
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



        // SIMULATION FIELDS 
        private bool isSimulating = false; // check to see if the program running a simulation
        private int selectedStationID; // selected station ID from UI
        private int selectedWorkerID; // selected worker ID from UI
        // private Dictionary<int, string> workerSkillNames = new();  // create a dictionary to map worker ID's to the name of their skill levels
        private DispatcherTimer simulationTimer; // timer for simulation
        private Workstation? selectedWorkstation; // store selected work station as a Workstation object
        private Worker? selectedWorker; // store selected worker as a worker object

        private bool waitingForRunner = false;

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


            Logger.Log($"Simulation STARTED | Timescale: {timescale:F1}x | Worker Cycle Time: {workerAssemblyTime:F1}s | Timer Interval: {cycleInterval:F1}s");
        }

        /// <summary>
        /// Handles each simulation timer tick.
        /// Checks if the order is complete, waits for runner refill if bins are empty,
        /// and runs a new lamp cycle when production can continue.
        /// </summary>
        /// <param name="sender">object that raised the timer event</param>
        /// <param name="e">event data for the timer tick</param>
        private async void SimulationTimer_Tick(object sender, EventArgs e)
        {
            // stop all simulation if the production goal has been reached
            if (await IsProductionCompleteAsync())
            {
                Logger.Log("ORDER COMPLETE - Simulation stopped.");
                StopSimulation();
                return;
            }

            // check if the current station still has enough parts to continue
            bool hasParts = await CheckBinsHavePartsAsync();

            // if there are no parts left, pause and wait for runner refill
            if (!hasParts)
            {
                // only log the waiting message once until refill happens
                if (!waitingForRunner)
                {
                    waitingForRunner = true;
                    Logger.Log($"WAITING FOR RUNNER - Station {selectedStationID} is out of parts.");
                }

                return; // skip this cycle and keep waiting
            }

            // if parts are back after waiting, resume the simulation
            if (waitingForRunner)
            {
                waitingForRunner = false;
                Logger.Log($"RUNNER REFILL DETECTED - Resuming production at Station {selectedStationID}.");
            }

            // run the next lamp cycle
            await RunLampCycleAsync();
        }


        /// <summary>
        /// Calls the refill stored procedure for a specific bin.
        /// This is used by the runner logic to replenish parts.
        /// </summary>
        /// <param name="binId">the ID of the bin to refill</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task RefillBinAsync(int binId)
        {
            // connect to the database
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open connection asynchronously

                // call the refill stored procedure
                using (SqlCommand cmd = new SqlCommand("sp_RefillBin", connection))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@BinID", binId); // pass the bin ID
                    await cmd.ExecuteNonQueryAsync(); // run the refill
                }
            }
        }

        /// <summary>
        /// Checks whether a tray has reached its capacity.
        /// If the tray is full, it sends the tray for testing by calling the tray processing stored procedure.
        /// </summary>
        /// <param name="trayId">the ID of the tray to check</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task ProcessFullTrayIfNeededAsync(int trayId)
        {
            // connect to the database
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open connection asynchronously

                // query the tray capacity and current number of lamps in it
                string query = @"
                    SELECT 
                        t.Capacity,
                        COUNT(l.LampID) AS LampCount
                    FROM Tray t
                    LEFT JOIN Lamps l ON t.TrayID = l.TrayID
                    WHERE t.TrayID = @TrayID
                    GROUP BY t.Capacity;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@TrayID", trayId); // pass selected tray ID

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        // if tray data exists, compare lamp count to tray capacity
                        if (await reader.ReadAsync())
                        {
                            int capacity = reader.GetInt32(reader.GetOrdinal("Capacity"));
                            int lampCount = reader.GetInt32(reader.GetOrdinal("LampCount"));

                            // if tray is full, process it for testing
                            if (lampCount >= capacity)
                            {
                                reader.Close(); // close reader before running another command

                                // call stored procedure to process the full tray
                                using (SqlCommand processCmd = new SqlCommand("sp_ProcessFullTray", connection))
                                {
                                    processCmd.CommandType = System.Data.CommandType.StoredProcedure;
                                    processCmd.Parameters.AddWithValue("@TrayID", trayId);
                                    await processCmd.ExecuteNonQueryAsync();
                                }

                                // log tray event
                                Logger.Log($"TRAY FULL - Tray {trayId} sent to testing.");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether the production order goal has been reached.
        /// Compares the total required good lamps to the number of passed lamps from quality inspection.
        /// </summary>
        /// <returns>True if the production goal has been reached, otherwise false.</returns>
        private async Task<bool> IsProductionCompleteAsync()
        {
            // connect to the database
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open connection asynchronously

                // get the total order goal and the number of good lamps that passed inspection
                string query = @"
                    SELECT 
                        CAST((SELECT ConfigValue FROM Configuration WHERE ConfigName = 'TotalOrderQuantity') AS INT) AS OrderGoal,
                        (SELECT COUNT(*) FROM QualityInspection WHERE IsDefective = 0) AS GoodLamps;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    // if values are returned, compare them
                    if (await reader.ReadAsync())
                    {
                        int orderGoal = reader.GetInt32(reader.GetOrdinal("OrderGoal"));
                        int goodLamps = reader.GetInt32(reader.GetOrdinal("GoodLamps"));

                        return goodLamps >= orderGoal; // true if production target is met
                    }
                }
            }

            return false; // default to false if something unexpected happens
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
                    cmd.Parameters.AddWithValue("@stationID", selectedStationID); // add parameter with value to SQL query

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
        /// Gets the current active tray ID from the database.
        /// Returns the first tray that is still available for production.
        /// </summary>
        /// <returns>The ID of the tray currently being used for production.</returns>
        private async Task<int> GetCurrentTrayIdAsync()
        {
            // connect to the database
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open connection asynchronously

                // get the first tray that is still available
                string query = @"
                    SELECT TOP 1 TrayID
                    FROM Tray
                    WHERE TrayStatus IN ('InUse', 'Empty')
                    ORDER BY TrayID;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    object result = await cmd.ExecuteScalarAsync();

                    // if a tray was found, return its ID
                    if (result != null)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }

            // throw an error if no tray is available
            throw new Exception("No available tray found.");
        }

        /// <summary>
        /// Runs one full lamp assembly cycle for the selected worker and workstation.
        /// Creates a barcode, gets the current tray, sends the cycle data to the database,
        /// and checks if the tray became full after the lamp was added.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task RunLampCycleAsync()
        {
            // get the currently selected worker and workstation from the UI
            Worker? selectedWorker = WorkerSelect.SelectedItem as Worker;
            Workstation? selectedStation = WorkstationSelect.SelectedItem as Workstation;

            // stop if either selection is missing
            if (selectedWorker == null || selectedStation == null)
            {
                MessageBox.Show("Please select a worker and workstation.");
                return;
            }

            // calculate cycle values for this lamp
            decimal assemblyTime = CalculateAssemblyTime(selectedWorker);
            bool isDefective = CalculateDefectResult(selectedWorker);
            string barcode = GenerateBarcode(selectedStation.StationID);

            try
            {
                // get the tray currently being used for production
                int currentTrayId = await GetCurrentTrayIdAsync();

                // log the start of the lamp cycle
                Logger.Log($"Starting lamp cycle | Barcode: {barcode} | Station: {selectedStation.StationID} | Worker: {selectedWorker.WorkerName} | Tray: {currentTrayId}");

                // connect to the database
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(); // open connection asynchronously

                    // call stored procedure to run the lamp cycle
                    using (SqlCommand cmd = new SqlCommand("sp_RunLampCycle", connection))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;

                        // pass all needed values into the stored procedure
                        cmd.Parameters.AddWithValue("@Barcode", barcode);
                        cmd.Parameters.AddWithValue("@StationID", selectedStation.StationID);
                        cmd.Parameters.AddWithValue("@WorkerID", selectedWorker.WorkerID);
                        cmd.Parameters.AddWithValue("@TrayID", currentTrayId);
                        cmd.Parameters.AddWithValue("@AssemblyTime", assemblyTime);
                        cmd.Parameters.AddWithValue("@IsDefective", isDefective ? 1 : 0);
                        //cmd.Parameters.AddWithValue("@Notes", isDefective ? "Fail" : "Pass");

                        await cmd.ExecuteNonQueryAsync(); // run the cycle
                    }
                }

                // after adding the lamp, check if the tray reached capacity
                await ProcessFullTrayIfNeededAsync(currentTrayId);

                // log successful completion of the lamp cycle
                Logger.Log($"Lamp cycle complete | {barcode} | Tray: {currentTrayId} | {assemblyTime:F1}s | {(isDefective ? "Fail" : "Pass")}");
            }
            catch (Exception ex)
            {
                // log any error that happens during the cycle
                Logger.Log($"ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a unique barcode using station ID and timestamp
        /// </summary>
        /// <param name="stationId"></param>
        /// <returns></returns>
        private string GenerateBarcode(int stationId)
        {
            return $"LAMP-S{stationId}-{DateTime.Now:yyyyMMddHHmmssfff}";
        }

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