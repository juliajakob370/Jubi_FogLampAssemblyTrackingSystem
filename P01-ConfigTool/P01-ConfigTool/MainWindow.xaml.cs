//------------------------------------------------------------------------------
// FILE          : MainWindow.xaml.cs
// PROJECT       : Project ManufacturingP01 - ConfigTool (Milestone 1)
// PROGRAMMER    : Bibi Murwared
// FIRST VERSION : 2026-03-11
// DESCRIPTION   : Code-behind for the configuration tool. Uses ADO.NET
//                 to load, add, edit, delete and reset configuration values
//                 stored in the Jubi database Configuration table.
//------------------------------------------------------------------------------
//https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-create-and-bind-to-an-observablecollection
//https://learn.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlcommand.executescalar?view=netframework-4.8.1
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
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

namespace P01_ConfigTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string connectionString;

        //in memory collection bound to the datagrid
        private ObservableCollection<ConfigurationItem> configurations;
        public MainWindow()
        {
            InitializeComponent();

            // get connection string from config file
            connectionString = ConfigurationManager.ConnectionStrings["JubiConnection"].ConnectionString;
            LoadConfigurations(); // load data from database into the collection and grid

        }

        /// <summary>
        /// loads all configuration records from the database into the observable collection
        /// and binds the collection to the datagrid
        /// </summary>
        private void LoadConfigurations()
        {
            //create a new collection each time we load
            configurations = new ObservableCollection<ConfigurationItem>();

            // open a sql connection to the our database
            using (SqlConnection connection = new SqlConnection(connectionString))

            //read all configuration rows
            using (SqlCommand command = new SqlCommand("SELECT ConfigID, ConfigName, ConfigValue, DataType, Description FROM Configuration ORDER BY ConfigName",connection))
            {
                connection.Open();

                //execute reader to walk through each row
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // create a new configuration object for each row
                        ConfigurationItem item = new ConfigurationItem
                        {
                            ConfigID = reader.GetInt32(0),
                            ConfigName = reader.GetString(1),
                            ConfigValue = reader.GetString(2),
                            DataType = reader.GetString(3),
                            Description = reader.IsDBNull(4) ? null : reader.GetString(4)
                        };

                        // add item to collection
                        configurations.Add(item);
                    }
                }
            }
            // bind the collection to the datagrid
            ConfigurationsDisplay.ItemsSource = configurations;
            DeleteButton.IsEnabled = false;  // disable delete button until a row is selected
        }
      
        /// <summary>
        /// handles selection changes in the datagrid and enables or disables the delete button
        /// </summary>
        /// <param name="sender">the datagrid that raised the event</param>
        /// <param name="e">event data with selection information</param>
        private void ConfigurationsDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // enable delete only when something is selected
            DeleteButton.IsEnabled = ConfigurationsDisplay.SelectedItem != null;
        }

        /// <summary>
        /// resets configuration values in the database back to the default values
        /// by calling the ResetConfigurationToDefaults stored procedure
        /// </summary>
        /// <param name="sender">the reset button</param>
        /// <param name="e">event data for the click</param>
        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            // ask the user to confirm the reset
            MessageBoxResult result = MessageBox.Show("Reset all configuration values to defaults?","Confirm reset", MessageBoxButton.YesNo,MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // call stored procedure to delete and reinsert default rows
            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand("ResetConfigurationToDefaults", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                connection.Open();
                command.ExecuteNonQuery();
            }

            // reload the fresh data from the database
            LoadConfigurations();
        }

        /// <summary>
        /// adds a new configuration row to the in memory collection
        /// (it is actually saved to the database when the user clicks Save) not sure if we should change it 
        /// </summary>
        /// <param name="sender">the add button</param>
        /// <param name="e">event data for the click</param>
        private void AddConfiguration_Click(object sender, RoutedEventArgs e)
        {
            // create a new config item with a generated name you will be able to edit it later 
            ConfigurationItem newItem = new ConfigurationItem
            {
                ConfigName = "New.Config." + DateTime.Now.Ticks,
                ConfigValue = "",
                DataType = "string",
                Description = "New configuration"
            };

            // add new item to the observable collection
            configurations.Add(newItem);

            //select and scroll to the new row so the user can edit it
            ConfigurationsDisplay.SelectedItem = newItem;
            ConfigurationsDisplay.ScrollIntoView(newItem);
        }


        /// <summary>
        /// closes the configuration tool window
        /// </summary>
        /// <param name="sender">the close button</param>
        /// <param name="e">event data for the click</param>
        private void Close_Click(object sender, RoutedEventArgs e)
        {   // any validation needed ?
            Close();

        }

        /// <summary>
        /// saves all changes from the observable collection back to the database
        /// inserts new rows and updates existing rows
        /// </summary>
        /// <param name="sender">the save button</param>
        /// <param name="e">event data for the click</param>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // open a single connection for all commands
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // loop through every configuration item in memory
                    foreach (ConfigurationItem item in configurations)
                    {
                        // if id is zero it means this is a new item that is not in the table yet
                        if (item.ConfigID == 0)
                        {
                            SqlCommand insertCommand = new SqlCommand(@"INSERT INTO Configuration (ConfigName, ConfigValue, DataType, Description)
                            VALUES(@name, @value, @type, @description); SELECT SCOPE_IDENTITY();", connection);

                            // pass values as parameters to avoid sql injection
                            insertCommand.Parameters.AddWithValue("@name", item.ConfigName);
                            insertCommand.Parameters.AddWithValue("@value", item.ConfigValue);
                            insertCommand.Parameters.AddWithValue("@type", item.DataType);
                            insertCommand.Parameters.AddWithValue("@description",(object?)item.Description ?? DBNull.Value);

                            //ExecuteScalar returns the new identity value
                            object newId = insertCommand.ExecuteScalar();
                            item.ConfigID = Convert.ToInt32(newId);
                        }
                        else
                        {
                            // existing row so we run an update statement
                            SqlCommand updateCommand = new SqlCommand(@"UPDATE Configuration SET ConfigValue = @value, DataType = @type, Description = @description WHERE ConfigID = @id;", connection);

                            updateCommand.Parameters.AddWithValue("@id", item.ConfigID);
                            updateCommand.Parameters.AddWithValue("@value", item.ConfigValue);
                            updateCommand.Parameters.AddWithValue("@type", item.DataType);
                            updateCommand.Parameters.AddWithValue("@description",(object?)item.Description ?? DBNull.Value);

                            updateCommand.ExecuteNonQuery();
                        }
                    }
                }
                // show the confirmation
                MessageBox.Show("Configurations saved.","Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception error)
            {
                //show error if something goeas wrong
                MessageBox.Show("Error saving configurations:\n" + error.Message,"Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// deletes the currently selected configuration from the database and from the in memory collection
        /// </summary>
        /// <param name="sender">the delete button</param>
        /// <param name="e">event data for the click</param>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            // cast the selected row to ConfigurationItem
            ConfigurationItem selectedItem = ConfigurationsDisplay.SelectedItem as ConfigurationItem;

            if (selectedItem == null)
            {
                return;
            }

            //confirm delete with the user
            MessageBoxResult result = MessageBox.Show($"Delete configuration '{selectedItem.ConfigName}'?","Confirm delete",MessageBoxButton.YesNo,MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // if the item already exists in the tabl then delete it from the database
            if (selectedItem.ConfigID != 0)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand command = new SqlCommand("DELETE from Configuration WHERE ConfigID = @id",connection))
                {
                    command.Parameters.AddWithValue("@id", selectedItem.ConfigID);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

            //remove the item from the observable collection
            configurations.Remove(selectedItem);
        }
    }
}