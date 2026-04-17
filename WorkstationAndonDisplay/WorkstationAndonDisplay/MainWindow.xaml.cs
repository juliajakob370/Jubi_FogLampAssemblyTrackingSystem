/*
 * FILE           : MainWindow.xaml.cs
 * PROJECT        : Project Manufacturing P01 > Workstation Andon Display
 * PROGRAMMERS    : Bibi Murwaredm Julia Jakob
 * FIRST VERSION  : 2026-03-27
 * DESCRIPTION    : Backend for the Workstation Andon display that shows
 *                  real-time bin levels, replenish status, yield,
 *                  tray information, and products produced for one workstation.
 */

using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;

namespace WorkstationAndonDisplay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string connectionString; // database connection string
        private DispatcherTimer? refreshTimer; // timer used to refresh the andon display
        private int selectedStationId = 0; // currently selected workstation
        private bool isRefreshing = false; // prevents overlapping refresh calls
        private bool isLoadingStations = false; // prevents selection-change loops while rebinding
        private bool hasShownConnectionError = false; // prevents endless popup spam

        /// <summary>
        /// Main constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            connectionString = ConfigurationManager.ConnectionStrings["JubiConnection"].ConnectionString;

            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Loads the Andon display window.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadActiveWorkstationsAsync();
            await RefreshAndonDisplayAsync();
            StartRefreshTimer();
        }

        /// <summary>
        /// Loads only the currently active workstations into the combo box.
        /// </summary>

        /// <summary>
        /// Loads only the currently active workstations into the combo box.
        /// </summary>
        private async Task LoadActiveWorkstationsAsync()
        {
            List<WorkstationItem> activeStations = new List<WorkstationItem>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            SELECT StationID, StationName
            FROM WorkStation
            WHERE Status = 'Active'
            ORDER BY StationName;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        activeStations.Add(new WorkstationItem
                        {
                            StationID = reader.GetInt32(0),
                            StationName = reader.GetString(1)
                        });
                    }
                }
            }

            int previousStationId = 0;

            if (WorkstationSelect.SelectedValue is int selectedValue)
            {
                previousStationId = selectedValue;
            }

            isLoadingStations = true;

            WorkstationSelect.ItemsSource = null;
            WorkstationSelect.Items.Clear();

            WorkstationSelect.DisplayMemberPath = "StationName";
            WorkstationSelect.SelectedValuePath = "StationID";
            WorkstationSelect.ItemsSource = activeStations;

            if (previousStationId > 0 && activeStations.Any(x => x.StationID == previousStationId))
            {
                WorkstationSelect.SelectedValue = previousStationId;
            }
            else if (activeStations.Count > 0)
            {
                WorkstationSelect.SelectedIndex = 0;
            }
            else
            {
                WorkstationSelect.SelectedIndex = -1;
                TitleText.Text = "No Active Workstations";
            }

            isLoadingStations = false;
        }

        /// <summary>
        /// Starts the timer that refreshes the Andon display every second.
        /// </summary>
        private void StartRefreshTimer()
        {
            refreshTimer?.Stop();

            refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            refreshTimer.Tick += async (s, e) =>
            {
                await RefreshAndonDisplayAsync();
            };

            refreshTimer.Start();
        }

        /// <summary>
        /// Refreshes all values for the currently selected workstation.
        /// </summary>
        /// <summary>
        /// Refreshes all values for the currently selected workstation.
        /// </summary>
        private async Task RefreshAndonDisplayAsync()
        {
            if (isRefreshing)
            {
                return;
            }

            isRefreshing = true;

            try
            {
                if (WorkstationSelect.SelectedValue == null)
                {
                    await LoadActiveWorkstationsAsync();
                }

                if (WorkstationSelect.SelectedValue == null)
                {
                    ClearDisplay();
                    return;
                }

                selectedStationId = Convert.ToInt32(WorkstationSelect.SelectedValue);

                await LoadStationHeaderAsync();
                await LoadBinLevelsAsync();
                await LoadProductionSummaryAsync();
                await LoadTraySummaryAsync();

                hasShownConnectionError = false;
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                // deadlock victim - skip this refresh cycle
            }
            catch (SqlException)
            {
                ClearDisplay();

                if (!hasShownConnectionError)
                {
                    hasShownConnectionError = true;
                    MessageBox.Show(
                        "Error refreshing Andon display.\nPlease check the database connection and try again.",
                        "Andon Display Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                ClearDisplay();

                if (!hasShownConnectionError)
                {
                    hasShownConnectionError = true;
                    MessageBox.Show(
                        "Unexpected error refreshing Andon display.",
                        "Andon Display Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                isRefreshing = false;
            }
        }
        /// <summary>
        /// Loads the station title for the selected workstation.
        /// </summary>
        private async Task LoadStationHeaderAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT StationName
                    FROM WorkStation
                    WHERE StationID = @StationID;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@StationID", selectedStationId);

                    object? result = await cmd.ExecuteScalarAsync();

                    if (result != null)
                    {
                        TitleText.Text = $"'{result}' Andon Display";
                    }
                    else
                    {
                        TitleText.Text = "Workstation Andon Display";
                    }
                }
            }
        }

        /// <summary>
        /// Loads the current bin levels for the selected workstation.
        /// </summary>
        private async Task LoadBinLevelsAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT 
                        p.PartName,
                        b.CurrentCount,
                        p.DefaultCapacity,
                        b.NeedsRefill
                    FROM Bin b
                    INNER JOIN Parts p ON b.PartID = p.PartID
                    WHERE b.StationID = @StationID
                    ORDER BY p.PartID;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@StationID", selectedStationId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string partName = reader["PartName"].ToString() ?? string.Empty;
                            int currentCount = Convert.ToInt32(reader["CurrentCount"]);
                            int defaultCapacity = Convert.ToInt32(reader["DefaultCapacity"]);
                            bool needsRefill = Convert.ToBoolean(reader["NeedsRefill"]);

                            double percent = 0;

                            if (defaultCapacity > 0)
                            {
                                percent = (double)currentCount / defaultCapacity * 100.0;
                            }

                            UpdatePartDisplay(partName, currentCount, percent, needsRefill);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates one part row in the UI.
        /// </summary>
        private void UpdatePartDisplay(string partName, int currentCount, double percent, bool needsRefill)
        {
            percent = Math.Max(0, Math.Min(100, percent));

            Brush statusBrush = needsRefill ? Brushes.Red : Brushes.LightGreen;

            switch (partName)
            {
                case "Harness":
                    HarnessBar.Value = percent;
                    HarnessCount.Text = currentCount.ToString();
                    HarnessStatus.Fill = statusBrush;
                    break;

                case "Reflector":
                    ReflectorBar.Value = percent;
                    ReflectorCount.Text = currentCount.ToString();
                    ReflectorStatus.Fill = statusBrush;
                    break;

                case "Housing":
                    HousingBar.Value = percent;
                    HousingCount.Text = currentCount.ToString();
                    HousingStatus.Fill = statusBrush;
                    break;

                case "Lens":
                    LensBar.Value = percent;
                    LensCount.Text = currentCount.ToString();
                    LensStatus.Fill = statusBrush;
                    break;

                case "Bulb":
                    BulbBar.Value = percent;
                    BulbCount.Text = currentCount.ToString();
                    BulbStatus.Fill = statusBrush;
                    break;

                case "Bezel":
                    BezelBar.Value = percent;
                    BezelCount.Text = currentCount.ToString();
                    BezelStatus.Fill = statusBrush;
                    break;
            }
        }

        /// <summary>
        /// Loads production summary values for the selected workstation.
        /// </summary>
        private async Task LoadProductionSummaryAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        COUNT(l.LampID) AS ProductsProduced,
                        CASE
                            WHEN COUNT(qi.InspectionID) = 0 THEN 0
                            ELSE CAST(
                                SUM(CASE WHEN qi.IsDefective = 0 THEN 1 ELSE 0 END) * 100.0
                                / COUNT(qi.InspectionID)
                                AS DECIMAL(5,2)
                            )
                        END AS YieldPercent
                    FROM WorkStation ws
                    LEFT JOIN Lamps l ON ws.StationID = l.StationID
                    LEFT JOIN QualityInspection qi ON l.LampID = qi.LampID
                    WHERE ws.StationID = @StationID
                    GROUP BY ws.StationID;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@StationID", selectedStationId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int produced = Convert.ToInt32(reader["ProductsProduced"]);
                            decimal yield = Convert.ToDecimal(reader["YieldPercent"]);

                            TotalProducedText.Text = $"Total: {produced}";
                            YieldText.Text = $"Yield: {yield:F2}%";
                        }
                        else
                        {
                            TotalProducedText.Text = "Total: 0";
                            YieldText.Text = "Yield: 0.00%";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads current tray and tray progress for the selected workstation.
        /// </summary>
        private async Task LoadTraySummaryAsync()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT TOP 1
                        t.TrayID,
                        t.Capacity,
                        COUNT(l.LampID) AS CurrentCount
                    FROM Tray t
                    LEFT JOIN Lamps l ON t.TrayID = l.TrayID AND l.StationID = @StationID
                    WHERE t.TrayStatus IN ('InUse', 'Empty')
                    GROUP BY t.TrayID, t.Capacity
                    ORDER BY t.TrayID;";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@StationID", selectedStationId);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int trayId = Convert.ToInt32(reader["TrayID"]);
                            int capacity = Convert.ToInt32(reader["Capacity"]);
                            int currentCount = Convert.ToInt32(reader["CurrentCount"]);

                            CurrentTrayText.Text = $"Tray: {trayId}";
                            TrayProgressText.Text = $"Tray Progress: {currentCount} / {capacity}";
                        }
                        else
                        {
                            CurrentTrayText.Text = "Tray: N/A";
                            TrayProgressText.Text = "Tray Progress: 0 / 0";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears the display when no active workstation is selected.
        /// </summary>
        private void ClearDisplay()
        {
            TitleText.Text = "No Active Workstations";

            HarnessBar.Value = 0;
            ReflectorBar.Value = 0;
            HousingBar.Value = 0;
            LensBar.Value = 0;
            BulbBar.Value = 0;
            BezelBar.Value = 0;

            HarnessCount.Text = "0";
            ReflectorCount.Text = "0";
            HousingCount.Text = "0";
            LensCount.Text = "0";
            BulbCount.Text = "0";
            BezelCount.Text = "0";

            HarnessStatus.Fill = Brushes.LightGray;
            ReflectorStatus.Fill = Brushes.LightGray;
            HousingStatus.Fill = Brushes.LightGray;
            LensStatus.Fill = Brushes.LightGray;
            BulbStatus.Fill = Brushes.LightGray;
            BezelStatus.Fill = Brushes.LightGray;

            YieldText.Text = "Yield: 0.00%";
            TotalProducedText.Text = "Total: 0";
            CurrentTrayText.Text = "Tray: N/A";
            TrayProgressText.Text = "Tray Progress: 0 / 0";
        }

        /// <summary>
        /// Handles workstation selection change.
        /// </summary>
        /// <summary>
        /// Handles workstation selection change.
        /// </summary>
        private async void WorkstationSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoadingStations)
            {
                return;
            }

            if (WorkstationSelect.SelectedValue is int stationId)
            {
                selectedStationId = stationId;
                await RefreshAndonDisplayAsync();
            }
        }

        /// <summary>
        /// Handles close button click.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            refreshTimer?.Stop();
            Close();
        }

        /// <summary>
        /// Simple class used for workstation combo box binding.
        /// </summary>
        private class WorkstationItem
        {
            public int StationID { get; set; }
            public string StationName { get; set; } = string.Empty;
        }


    }
}