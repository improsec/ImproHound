using ImproHound.pages;
using System.Windows;
using System.Windows.Controls;

namespace ImproHound
{
    public partial class ImproHoundWindow : Window
    {
        public ImproHoundWindow()
        {
            InitializeComponent();
            MainWindow.SetWindow(this);
            MainFrame.Navigate(new ConnectPage());
        }

        public void NavigateToPage(Page page)
        {
            MainFrame.Navigate(page);

            switch (page.Title)
            {
                case "OUStructurePage":
                    ResizeMode = ResizeMode.CanResize;
                    MinHeight = 400;
                    MinWidth = 700;
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
