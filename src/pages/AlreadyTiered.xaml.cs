using System.Windows;
using System.Windows.Controls;

namespace ImproHound.pages
{
    public partial class AlreadyTieredPage : Page
    {
        private readonly MainWindow containerWindow;
        private readonly DBConnection connection;
        private readonly ConnectPage connectPage;

        public AlreadyTieredPage(MainWindow containerWindow, DBConnection connection, ConnectPage connectPage)
        {
            this.containerWindow = containerWindow;
            this.connection = connection;
            this.connectPage = connectPage;

            InitializeComponent();
        }

        private void StartoverButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            containerWindow.NavigateToPage(new OUStructurePage(containerWindow, connection, connectPage, alreadyTiered: true, startover: true));
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            containerWindow.NavigateToPage(new OUStructurePage(containerWindow, connection, connectPage, alreadyTiered: true, startover: false));
        }
    }
}
