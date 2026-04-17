/*
 * FILE           : MainWindow.xaml.cs
 * PROJECT        : Project Manufacturing - RunnerDisplay
 * PROGRAMMERS    : Bibi Murwared
 * FIRST VERSION  : 2026-04-15
 * DESCRIPTION    : Backend for the Runner Display program.
 *                  Shows runner log entries in the UI and refills bins
 *                  every 5 simulated minutes while the runner is active.
 */

using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace RunnerDisplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string connectionString; // store DB connection string
        private DispatcherTimer? runnerTimer; // timer used to run scheduled runner visits
        private bool isRunnerActive = false; // track whether the runner is currently running

        private ObservableCollection<Notification> notifications = new ObservableCollection<Notification>(); // scrolling UI log
        private HashSet<int> activeLowStockBins = new HashSet<int>(); // track bins that currently still need refill

        private DispatcherTimer? watcherTimer; // fast watcher for new red alerts

        /// <summary>
        /// Main constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // get connection string from App.config
            connectionString = ConfigurationManager.ConnectionStrings["JubiConnection"].ConnectionString;

            // bind the grid to the notification collection
            NotificationGrid.ItemsSource = notifications;

            // initialize runner logger
            RunnerLogger.Initialize();
            RunnerLogger.Log("Runner Display window opened.");

            // load initial low stock state when window opens
            Loaded += MainWindow_Loaded;

            // prevent X button close while runner is active
            Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Starts a fast watcher timer so new low stock bins show as red alerts right away.
        /// This only watches for red notifications and does not refill anything.
        /// </summary>
        private void StartWatcherTimer()
        {
            watcherTimer?.Stop();

            watcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // check often for immediate red alerts
            };

            watcherTimer.Tick += async (s, e) =>
            {
                await CheckForNewLowStockNotificationsAsync();
                UpdateNotificationDot();
            };

            watcherTimer.Start();
        }

        /// <summary>
        /// Loads current low stock bins when the window first opens.
        /// Starts the watcher so red alerts appear right away.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RunnerLogger.Log("Main window loaded. Checking for current low stock bins.");
            await CheckForNewLowStockNotificationsAsync();
            UpdateNotificationDot();
        }

        /// <summary>
        /// Starts the scheduled runner visit timer using the simulation time scale.
        /// The runner only refills bins on timer visits every 5 simulated minutes.
        /// </summary>
        private async Task StartRunnerAsync()
        {
            // check current low stock bins first before starting timer
            await CheckForNewLowStockNotificationsAsync();

            // get simulation time scale from the Configuration table
            decimal timeScale = await GetSimulationTimeScaleAsync();
            RunnerLogger.Log("Runner START requested.");
            RunnerLogger.Log($"SimulationTimeScale loaded: {timeScale}");

            // protect against invalid values
            if (timeScale <= 0)
            {
                timeScale = 1;
                RunnerLogger.Log("Invalid SimulationTimeScale found. Defaulted to 1.");
            }

            // 5 simulated minutes = 300 simulation seconds
            double realSeconds = 300.0 / (double)timeScale;

            // protect against timer being too fast
            if (realSeconds < 1)
            {
                realSeconds = 1;
                RunnerLogger.Log("Calculated interval was too small. Defaulted to 1 second.");
            }

            // create scheduled runner timer
            runnerTimer?.Stop();
            runnerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(realSeconds)
            };

            runnerTimer.Tick += RunnerTimer_Tick;
            runnerTimer.Start();

            isRunnerActive = true;

            RunnerLogger.Log($"Runner visit interval set to {realSeconds:F2} real seconds (every 5 simulated minutes).");
            RunnerLogger.Log("Runner timer started.");

            // update button states
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        /// <summary>
        /// Stops the scheduled runner timer and updates button states.
        /// The watcher can keep running if you still want immediate red alerts while window is open.
        /// </summary>
        private void StopRunner()
        {
            RunnerLogger.Log("Runner STOP requested.");

            runnerTimer?.Stop();
            isRunnerActive = false;

            StopButton.IsEnabled = false;
            StartButton.IsEnabled = true;

            RunnerLogger.Log("Runner timer stopped.");
        }

        /// <summary>
        /// Handles one scheduled runner visit.
        /// On each visit the runner refills all currently flagged bins
        /// and adds green log entries for the refill actions.
        /// </summary>
        private async void RunnerTimer_Tick(object? sender, EventArgs e)
        {
            RunnerLogger.Log("Runner visit started.");

            // first check for any bins that are currently low and add red logs if needed
            await CheckForNewLowStockNotificationsAsync();

            // then refill all bins that are currently flagged
            await RefillAllLowStockBinsAsync();

            // update the dot after this runner visit
            UpdateNotificationDot();

            RunnerLogger.Log("Runner visit finished.");
        }

        /// <summary>
        /// Gets SimulationTimeScale from the Configuration table.
        /// </summary>
        /// <returns>Simulation time scale as decimal</returns>
        private async Task<decimal> GetSimulationTimeScaleAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open database connection

                string query = @"
                    SELECT ConfigValue
                    FROM Configuration
                    WHERE ConfigName = 'SimulationTimeScale';";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    object? result = await cmd.ExecuteScalarAsync();

                    if (result == null)
                    {
                        return 1;
                    }

                    return Convert.ToDecimal(result);
                }
            }
        }

        /// <summary>
        /// Checks the database for bins that currently need refill.
        /// Adds a red log entry only the first time each bin becomes low.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task CheckForNewLowStockNotificationsAsync()
        {
            // store low stock bins currently returned from the database
            List<(int BinID, string PartName, string StationName)> currentLowBins = new List<(int, string, string)>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open database connection

                using (SqlCommand cmd = new SqlCommand("sp_GetLowStockBins", connection))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            currentLowBins.Add
                            (
                                (
                                    Convert.ToInt32(reader["BinID"]),
                                    reader["PartName"].ToString() ?? string.Empty,
                                    reader["StationName"].ToString() ?? string.Empty
                                )
                            );
                        }
                    }
                }
            }

            // add a red log only if that bin was not already marked as active low stock
            foreach (var bin in currentLowBins)
            {
                if (!activeLowStockBins.Contains(bin.BinID))
                {
                    activeLowStockBins.Add(bin.BinID);

                    // insert new low stock log at the top of the grid
                    notifications.Insert(0, new Notification
                    {
                        PartName = bin.PartName,
                        BinID = bin.BinID,
                        Location = bin.StationName,
                        TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        StatusColor = Brushes.Red
                    });

                    RunnerLogger.Log($"LOW STOCK DETECTED | Part: {bin.PartName} | BinID: {bin.BinID} | Location: {bin.StationName}");
                }
            }

            // update the top-right dot after checking
            UpdateNotificationDot();
        }

        /// <summary>
        /// Refills all bins that currently have NeedsRefill = 1.
        /// This method runs only during the scheduled runner visit (every 5 simulated minutes).
        /// It logs the start, each refill action, and the end of the runner cycle.
        /// </summary>
        private async Task RefillAllLowStockBinsAsync()
        {
            // log start of refill phase (this is the runner "visit")
            RunnerLogger.Log("RUNNER REFILL PHASE STARTED.");

            // temporary list of bins that currently need refill
            List<(int BinID, string PartName, string StationName)> binsToRefill = new List<(int, string, string)>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // open database connection

                // get all bins that are flagged for refill at the time of the runner visit
                string query = @"
            SELECT 
                b.BinID,
                p.PartName,
                ws.StationName
            FROM Bin b
            INNER JOIN Parts p ON b.PartID = p.PartID
            INNER JOIN WorkStation ws ON b.StationID = ws.StationID
            WHERE b.NeedsRefill = 1
            ORDER BY ws.StationName, p.PartName;";

                using (SqlCommand getCmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await getCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        binsToRefill.Add
                        (
                            (
                                Convert.ToInt32(reader["BinID"]),
                                reader["PartName"].ToString() ?? string.Empty,
                                reader["StationName"].ToString() ?? string.Empty
                            )
                        );
                    }
                }

                // log how many bins were found needing refill
                RunnerLogger.Log($"RUNNER FOUND {binsToRefill.Count} BIN(S) NEEDING REFILL.");

                // if nothing to refill, log and exit early
                if (binsToRefill.Count == 0)
                {
                    RunnerLogger.Log("RUNNER VISIT COMPLETE - No bins required refill.");
                    return;
                }

                // refill each flagged bin and add a green log entry
                foreach (var bin in binsToRefill)
                {
                    using (SqlCommand refillCmd = new SqlCommand("sp_RefillBin", connection))
                    {
                        refillCmd.CommandType = System.Data.CommandType.StoredProcedure;
                        refillCmd.Parameters.AddWithValue("@BinID", bin.BinID);
                        await refillCmd.ExecuteNonQueryAsync();
                    }

                    // remove the bin from the active low stock set because it has now been refilled
                    activeLowStockBins.Remove(bin.BinID);

                    // add green refill log at the top of the UI grid
                    notifications.Insert(0, new Notification
                    {
                        PartName = bin.PartName,
                        BinID = bin.BinID,
                        Location = bin.StationName,
                        TimeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        StatusColor = Brushes.Green
                    });

                    // log each individual refill
                    RunnerLogger.Log($"BIN REFILLED | Part: {bin.PartName} | BinID: {bin.BinID} | Location: {bin.StationName}");
                }
            }

            // log end of refill phase
            RunnerLogger.Log("RUNNER REFILL PHASE COMPLETED.");

            // update the top-right dot after refill
            UpdateNotificationDot();
        }

        /// <summary>
        /// Updates the small notification dot in the header.
        /// Red means at least one active bin still needs refill.
        /// LightGray means there are no active refill needs right now.
        /// </summary>
        private void UpdateNotificationDot()
        {
            if (activeLowStockBins.Count > 0)
            {
                NotificationDot.Fill = Brushes.Red; // active refill needed
            }
            else
            {
                NotificationDot.Fill = Brushes.LightGray; // no active refill needed
            }
        }

        /// <summary>
        /// Handles Start button click and starts the runner timer.
        /// </summary>
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await StartRunnerAsync();
        }

        /// <summary>
        /// Handles Stop button click and stops the runner timer.
        /// </summary>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRunner();
        }

        /// <summary>
        /// Handles Close button click.
        /// Forces the user to stop the runner before closing.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RunnerLogger.Log("Close button clicked.");

            if (isRunnerActive)
            {
                RunnerLogger.Log("Close blocked because runner is still active.");
                MessageBox.Show("Please stop runner before closing.", "Runner Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunnerLogger.Log("Runner Display window closed.");
            Close();
        }
        /// <summary>
        /// Handles X button close.
        /// Prevents closing while the runner is active.
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isRunnerActive)
            {
                MessageBox.Show("Please stop runner before closing.", "Runner Active", MessageBoxButton.OK, MessageBoxImage.Warning);
                RunnerLogger.Log("Window close blocked by X button because runner is still active.");
                e.Cancel = true;
            }
        }
    }
}