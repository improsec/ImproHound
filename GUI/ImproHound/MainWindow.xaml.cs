using ImproHound.pages;
using System.Windows;
using System.Windows.Controls;

namespace ImproHound
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new ConnectPage(this));
        }

        public void NavigateToPage(Page page)
        {
            MainFrame.Navigate(page);

            switch (page.Title)
            {
                case "OUStructurePage":
                    ResizeMode = ResizeMode.CanResize;
                    MinHeight = 400;
                    MinWidth = 600;
                    break;

                default:
                    ResizeMode = ResizeMode.CanMinimize;
                    Width = page.Width;
                    Height = page.Height;
                    break;
            }
        }
    }
}
