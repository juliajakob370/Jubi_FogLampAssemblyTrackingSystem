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
        /// Get the most recent values from the Kanban Summary View in the DB and update the display
        /// </summary>
        private void RefreshKanban()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // ==============================================
                // 1. MAIN SUMMARY (get from the view in the DB)
                // ==============================================
                SqlCommand command = new SqlCommand("SELECT * FROM vwKanbanSummary", conn);
                SqlDataReader reader = command.ExecuteReader();

                // first read the values you need
                if (reader.Read())
                {
                    int order = Convert.ToInt32(reader["OrderAmount"]);
                    int good = Convert.ToInt32(reader["GoodLamps"]);
                    int total = Convert.ToInt32(reader["TotalProduced"]);
                    double yield = Convert.ToDouble(reader["YieldPercentage"]);

                    // update the displays
                    OrderAmountText.Text = order.ToString();
                    OrderProgressText.Text = $"{good} / {order}";
                    LampsProducedText.Text = total.ToString();
                    TotalYieldText.Text = $"{yield:F2}%";
                }

                reader.Close();

                // ===========================================
                // 2. FIND THE NUMBER OF RUNNING WORKSTATIONS
                // ===========================================
                SqlCommand wsCommand = new SqlCommand("SELECT COUNT(*) FROM WorkStation WHERE Status = 'Active'", conn);

                RunningWorkstationsText.Text = wsCommand.ExecuteScalar().ToString(); // update the display

                // ==================================
                // 3. YIELD PER STATION CALCULATION
                // ==================================
                string yieldQuery = @"SELECT ws.StationName, CASE 
                    WHEN COUNT(qi.InspectionID) = 0 THEN 0
                    ELSE CAST(SUM(CASE WHEN qi.IsDefective = 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(qi.InspectionID) 
                    AS DECIMAL(5,2))
                    END AS Yield
                    FROM WorkStation ws
                    LEFT JOIN Lamps l ON ws.StationID = l.StationID
                    LEFT JOIN QualityInspection qi ON l.LampID = qi.LampID
                    GROUP BY ws.StationName;";

                SqlCommand yieldCommand = new SqlCommand(yieldQuery, conn);
                SqlDataReader yieldReader = yieldCommand.ExecuteReader();

                WorkstationYieldList.Items.Clear();

                while (yieldReader.Read())
                {
                    string station = yieldReader["StationName"].ToString();
                    double y = Convert.ToDouble(yieldReader["Yield"]);

                    WorkstationYieldList.Items.Add($"{station}: {y:F2}%");
                }

                yieldReader.Close();
            }
        }
    }
}