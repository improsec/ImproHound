using System.Windows.Controls;

namespace ImproHound
{
    public static class MainWindow
    {
        private static ImproHoundWindow window = null;
        private static pages.ConnectPage connectPage = null;
        public static void SetWindow(ImproHoundWindow value)
        {
            window = value;
        }
        public static void SetConnectPage(pages.ConnectPage value)
        {
            connectPage = value;
        }

        public static void BackToConnectPage()
        {
            window.NavigateToPage(connectPage);
        }

        public static void NavigateToPage(Page page)
        {
            window.NavigateToPage(page);
        }
    }
}

