/*
 * FILE           : MainWindow.xaml.cs
 * PROJECT        : Project Manufacturing P01 > Assembly Line Kanban
 * PROGRAMMERS    : Julia Jakob & Bibi Murwared
 * FIRST VERSION  : 2026-03-27
 * DESCRIPTION    : Backend for the Assembly Line Kanban display that shows
 *                  real time production data including order progress,
 *                  total production, yield, and running workstations.
 */
using System.Configuration;
using System.Data.SqlClient;
using System.Text;
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
using Microsoft.Data.SqlClient;

namespace AssemblyLineKanban
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly string connectionString; // create variable to store connection string from app.config
        public MainWindow()
        {
            InitializeComponent();
            connectionString = ConfigurationManager.ConnectionStrings["JubiConnection"].ConnectionString; // get the connection string from the app.config
            StartAutoRefresh();
        }

        /// <summary>
        /// To close the screen with the close button
        /// </summary>
        /// <param name="sender">the object that raised the event</param>
        /// <param name="e">event details</param>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Start the dispatch timer and set it's action to be to refresh the values every second
        /// </summary>
        private void StartAutoRefresh()
        {
            DispatcherTimer timer = new DispatcherTimer(); // use a dispatch timer to control refresh
            timer.Interval = TimeSpan.FromSeconds(1); // refresh display every 1 sec
            timer.Tick += (s, e) => RefreshKanban();
            timer.Start();
        }

        /// <summary>
        /// Gets the most recent values from the database and updates the Kanban display.
        /// Handles temporary database deadlocks by skipping that refresh cycle.
        /// </summary>
        private void RefreshKanban()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // ==============================================
                    // 1. MAIN SUMMARY (get from the view in the DB)
                    // ==============================================
                    using (SqlCommand command = new SqlCommand("SELECT * FROM vwKanbanSummary", conn))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int order = Convert.ToInt32(reader["OrderAmount"]);
                            int good = Convert.ToInt32(reader["GoodLamps"]);
                            int total = Convert.ToInt32(reader["TotalProduced"]);
                            double yield = Convert.ToDouble(reader["YieldPercentage"]);

                            OrderAmountText.Text = order.ToString();
                            OrderProgressText.Text = $"{good} / {order}";
                            LampsProducedText.Text = total.ToString();
                            TotalYieldText.Text = $"{yield:F2}%";
                        }
                    }

                    // ===========================================
                    // 2. FIND THE NUMBER OF RUNNING WORKSTATIONS
                    // ===========================================
                    string runningStationsQuery = @"
                SELECT COUNT(*)
                FROM WorkStation
                WHERE Status = 'Active';";

                    using (SqlCommand wsCommand = new SqlCommand(runningStationsQuery, conn))
                    {
                        object? result = wsCommand.ExecuteScalar();
                        RunningWorkstationsText.Text = result?.ToString() ?? "0";
                    }

                    // ==================================
                    // 3. YIELD PER STATION CALCULATION
                    // ==================================
                    string yieldQuery = @"SELECT ws.StationName, CASE 
                        WHEN COUNT(qi.InspectionID) = 0 THEN 0
                        ELSE CAST(
                            SUM(CASE WHEN qi.IsDefective = 0 THEN 1 ELSE 0 END) * 100.0 
                            / COUNT(qi.InspectionID)
                            AS DECIMAL(5,2))
                        END AS Yield
                        FROM WorkStation ws
                        LEFT JOIN Lamps l ON ws.StationID = l.StationID
                        LEFT JOIN QualityInspection qi ON l.LampID = qi.LampID
                        GROUP BY ws.StationName
                        ORDER BY ws.StationName;";

                    using (SqlCommand yieldCommand = new SqlCommand(yieldQuery, conn))
                    using (SqlDataReader yieldReader = yieldCommand.ExecuteReader())
                    {
                        WorkstationYieldList.Items.Clear();

                        while (yieldReader.Read())
                        {
                            string station = yieldReader["StationName"].ToString() ?? string.Empty;
                            double y = Convert.ToDouble(yieldReader["Yield"]);

                            WorkstationYieldList.Items.Add($"{station}: {y:F2}%");
                        }
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                // 1205 = deadlock victim
                // skip this refresh cycle and try again on the next timer tick
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error refreshing Kanban:\n" + ex.Message,
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}