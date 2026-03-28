/*
 * FILE           : MainWindow.xaml.cs
 *  PROJECT       : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob
 *  FIRST VERSION : 2026-03-27
 *  DESCRIPTION   : The backend for the Workstation Simulation program that will update the database 
 *                  to simulate work flow
 *  REFERENCES    : https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer?view=windowsdesktop-10.0
 *                  https://wpf-tutorial.com/misc/dispatchertimer/
*/
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Data.SqlClient;
using System.Threading.Tasks;


// Program flow - pseudo code so i can better do this
// - don't need to worry about restocking or defect rates yet - just timing
// User opens workstation simulator
// Load in the available Workers from the Workers Table
// Load in the available Workstations from the Workstations table
// User selects one Worker and one Workstation from the dropdowns
// When they have selected options from both drop downs - start button is enabled
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
        private DispatcherTimer configRefreshTimer; // dispatch timer for the cofig refresh - so that the config tool can actually update the values being used
        private bool isSimulating = false; // check to see if the program running a simulation

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
        /// <returns>Task</returns>
        private async Task LoadWorkersAsync()
        {
            var workers = new List<Worker>(); // make a new list of Workers

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); // access the db asynchronously

                string query = @"SELECT WorkerID, WorkerName FROM Worker ORDER BY WorkerName;"; // make the query

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    // read in workers create worker objects and add them to the list of workers
                    while (await reader.ReadAsync())
                    {
                        workers.Add(new Worker
                        {
                            WorkerID = reader.GetInt32(0),
                            WorkerName = reader.GetString(1)
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
        /// <returns>Task</returns>
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
        /// <returns>Task</returns>
        private async Task LoadConfigurationAsync()
        {
            // Load initial values
            await RefreshConfigFromDatabaseAsync();

            // Start refresh timer (every 5 seconds while the simulation is not happening)
            configRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
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
        /// Refresh config values so the simulation can still get the most up to date config values
        /// </summary>
        /// <returns>task</returns>
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
        private decimal GetConfigDecimal(string key)
        {
            if (configValues.ContainsKey(key))
            {
                return decimal.Parse(configValues[key]);
            }
            else
            {
                return 0m; // 0m for decimal
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


        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Workstation selectedWorkstation = WorkstationSelect.SelectedItem as Workstation;
            Worker selectedWorker = WorkerSelect.SelectedItem as Worker;

            // Initialize the logs
            Logger.Initialize(selectedWorkstation.StationName);

            isSimulating = true; // set that the simulation has started
            configRefreshTimer?.Stop(); // stop checking for config updates

            // log that the simulation has started with the selected worker and workstation
            Logger.Log($"Simulation STARTED - Worker: {selectedWorker.WorkerName}, Station: {selectedWorkstation.StationName}");

            // UI disabling / enabling logic
            StopButton.IsEnabled = true;
            StartButton.IsEnabled = false;
            WorkerSelect.IsEnabled = false;
            WorkstationSelect.IsEnabled = false;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
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