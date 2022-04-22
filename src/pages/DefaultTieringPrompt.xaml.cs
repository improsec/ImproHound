using System.Windows;
using System.Windows.Controls;

namespace ImproHound.pages
{
    public partial class DefaultTieringPromptPage : Page
    {
        private readonly DBAction dBAction;

        public DefaultTieringPromptPage(DBAction dBAction)
        {
            InitializeComponent();
            this.dBAction = dBAction;
        }

        private void DefaultTieringButton_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            MainWindow.NavigateToPage(new OUStructurePage(dBAction, NewTieringAction.DefaultTiering));
        }

        private void AllInTier2Button_Click(object sender, RoutedEventArgs e)
        {
            // Jump to OU structure page
            MainWindow.NavigateToPage(new OUStructurePage(dBAction, NewTieringAction.AllInTier2));
        }
    }
}
