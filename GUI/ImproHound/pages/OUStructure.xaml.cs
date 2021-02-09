using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class OUStructurePage : Page
    {
        public OUStructurePage()
        {
            InitializeComponent();

            List<OU> families = new List<OU>();

            OU family1 = new OU() { Name = "The Doe's" };
            family1.Members.Add(new User() { Name = "John Doe", Tier = 42 });
            family1.Members.Add(new User() { Name = "Jane Doe", Tier = 39 });
            family1.Members.Add(new User() { Name = "Sammy Doe", Tier = 13 });
            families.Add(family1);

            OU family2 = new OU() { Name = "The Moe's" };
            family2.Members.Add(new User() { Name = "Mark Moe", Tier = 31 });
            family2.Members.Add(new User() { Name = "Norma Moe", Tier = 28 });
            families.Add(family2);

            trvFamilies.ItemsSource = families;
        }
    }

    public class ADObject
    {
        public string Name { get; set; }
        public int Tier { get; set; }
    }

    public class OU : ADObject
    {
        public OU()
        {
            this.Members = new ObservableCollection<ADObject>();
        }

        public ObservableCollection<ADObject> Members { get; set; }
    }

    public class User : ADObject
    {
    }

    public class Tiers : List<String>
    {
        public Tiers()
        {
            AddRange(new List<String>() { "0", "1", "2" });
        }
    }
}
