using System;
using System.Collections.Generic;
using System.Linq;
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

namespace ImproHound.pages
{
    public partial class AlreadyTieredPage : Page
    {
        private readonly MainWindow containerWindow;
        private readonly DBConnection connection;
        private readonly ConnectPage connectPage;
        private readonly int numOfTierLabels;

        public AlreadyTieredPage(MainWindow containerWindow, DBConnection connection, ConnectPage connectPage, int numOfTierLabels)
        {
            this.containerWindow = containerWindow;
            this.connection = connection;
            this.connectPage = connectPage;
            this.numOfTierLabels = numOfTierLabels;
            InitializeComponent();
        }

        private void StartoverButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            containerWindow.NavigateToPage(new OUStructurePage(containerWindow, connection, connectPage, startover: true));
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            containerWindow.NavigateToPage(new OUStructurePage(containerWindow, connection, connectPage, startover: false, numOfTierLabels: numOfTierLabels));
        }
    }
}
