using ImproHound.pages;
using System.Windows;

namespace ImproHound
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new ConnectPage());
        }
    }
}
