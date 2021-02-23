using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ImproHound.pages
{
    public partial class ConnectPage : Page
    {
        MainWindow containerWindow;

        public ConnectPage(MainWindow containerWindow)
        {
            InitializeComponent();
            this.containerWindow = containerWindow;
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            int numOfTierLabels = 0;
            object output;

            // Make sure we can connect to the DB and the graph is not empty
            DBConnection connection = new DBConnection(url.Text, username.Text, password.Text);

            try
            {
                List<IRecord> response = await connection.Query("CALL apoc.meta.stats() YIELD labels RETURN labels");
                if (!response[0].Values.TryGetValue("labels", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong.\nNo authentication error but could not fetch number of nodes in graph.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DisableGUIWait();
                    return;
                }

                // Ensure graph is not empty
                Dictionary<string, object> dirout = (Dictionary<string, object>)output;
                object numOfBase;
                dirout.TryGetValue("Base", out numOfBase);
                Console.WriteLine("Number of nodes in graph: " + numOfBase);
                if (numOfBase.ToString().Equals("0"))
                {
                    // 0 nodes in graph error
                    MessageBox.Show("You have 0 nodes with label 'Base' in your graph.\nMake sure you have upload BloodHound data to graph before connecting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DisableGUIWait();
                    return;
                }

                // Get number of tier labels already existing in db
                numOfTierLabels = new List<string>(dirout.Keys).Where(key => key.StartsWith("Tier")).Count();
            }
            catch (Exception err)
            {
                // Error
                if (err.Message.ToString().StartsWith("There is no procedure with the name"))
                {
                    MessageBox.Show("Procedure 'apoc.meta.stats()' does not exist. Make sure APOC plugin is installed in database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                DisableGUIWait();
                return;
            }

            // Jump to OU structure page
            DisableGUIWait();
            containerWindow.NavigateToPage(new OUStructurePage(containerWindow, connection, this, numOfTierLabels: numOfTierLabels));
        }

        private void EnableGUIWait()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            url.IsEnabled = false;
            username.IsEnabled = false;
            password.IsEnabled = false;
            connectButton.IsEnabled = false;
        }

        private void DisableGUIWait()
        {
            Mouse.OverrideCursor = null;
            url.IsEnabled = true;
            username.IsEnabled = true;
            password.IsEnabled = true;
            connectButton.IsEnabled = true;
        }
    }
}
