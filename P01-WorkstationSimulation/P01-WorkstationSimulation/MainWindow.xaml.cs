/*
 * FILE           : MainWindow.xaml.cs
 *  PROJECT       : Project Manufacturing P01 - Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob
 *  FIRST VERSION : 2026-03-27
 *  DESCRIPTION   : The backend for the Workstation Simulation program that will update the database 
 *                  to simulate work flow
*/
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
        public MainWindow()
        {
            InitializeComponent();
        }



        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = true;
            StartButton.IsEnabled = false;

        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }
}