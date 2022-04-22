using System.Windows;
using System.Windows.Controls;

namespace ImproHound.pages
{
    public partial class AlreadyTieredPage : Page
    {
        public AlreadyTieredPage()
        {
            InitializeComponent();
        }

        private void StartoverButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to default tiering prompt page
            MainWindow.NavigateToPage(new DefaultTieringPromptPage(DBAction.StartOver));
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            MainWindow.NavigateToPage(new OUStructurePage(DBAction.Continue, NewTieringAction.Continue));
        }
    }
}
