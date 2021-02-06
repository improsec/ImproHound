using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Neo4j.Driver;

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

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            // Make sure we can connect to the DB and the graph is not empty
            object output;
            DBConnection connection = new DBConnection(url.Text, username.Text, password.Text);
            try
            {
                List<IRecord> testConnection = await connection.CypherQuery("MATCH (n) RETURN COUNT(n)");

                if (!testConnection[0].Values.TryGetValue("COUNT(n)", out output))
                {
                    MessageBox.Show("Something went wrong.\nNo authentication error but could not fetch number of nodes in graph.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                long numOfNodes = (long)output;
                Console.WriteLine("Number of nodes in graph: " + numOfNodes);
                if (numOfNodes.Equals(0))
                {
                    MessageBox.Show("You have 0 nodes in your graph.\nMake sure you have upload BloodHound data to graph before connecting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

            } catch (AuthenticationException err)
            {
                MessageBox.Show("Authentication error:\n" + err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Jump to OU structure page
            containerWindow.NavigateToPage(new OUStructurePage());
        }
    }
}
