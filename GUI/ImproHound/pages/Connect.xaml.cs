using System.Windows;
using System.Windows.Controls;

namespace ImproHound.pages
{
    public partial class ConnectPage : Page
    {
        public ConnectPage()
        {
            InitializeComponent();
        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            DBConnection greeter = new DBConnection("bolt://localhost:7687", "neo4j", "bloodhound");
            await greeter.asyncAsync("CREATE (a:Greeting) SET a.message = \"hi\" RETURN a.message");
        }
    }
}
