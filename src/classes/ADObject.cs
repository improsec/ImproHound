using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ImproHound.classes
{
    public class ADObject : INotifyPropertyChanged
    {
        readonly pages.OUStructurePage oUStructurePage;
        private string tier;

        public ADObject(string objectid, ADObjectType type, string cn, string name, string distinguishedname, string tier, pages.OUStructurePage oUStructurePage)
        {
            Objectid = objectid;
            CN = cn;
            Name = name;
            Distinguishedname = distinguishedname;
            Tier = tier;
            Type = type;
            Children = new Dictionary<string, ADObject>();
            this.oUStructurePage = oUStructurePage;

            TierUpCommand = new RelayCommand(TierUp);
            TierDownCommand = new RelayCommand(TierDown);

            switch (type)
            {
                case ADObjectType.Domain:
                    Iconpath = "/resources/images/ad-icons/domain1.png";
                    break;
                case ADObjectType.Container:
                    Iconpath = "/resources/images/ad-icons/container.png";
                    break;
                case ADObjectType.OU:
                    Iconpath = "/resources/images/ad-icons/ou.png";
                    break;
                case ADObjectType.Group:
                    Iconpath = "/resources/images/ad-icons/group.png";
                    break;
                case ADObjectType.User:
                    Iconpath = "/resources/images/ad-icons/user.png";
                    break;
                case ADObjectType.Computer:
                    Iconpath = "/resources/images/ad-icons/computer.png";
                    break;
                case ADObjectType.GPO:
                    Iconpath = "/resources/images/ad-icons/gpo.png";
                    break;
                default:
                    Iconpath = "/resources/images/ad-icons/domain2.png";
                    break;
            }
        }
        public string Objectid { get; set; }
        public string CN { get; set; }
        public string Name { get; set; }
        public string Distinguishedname { get; set; }
        public ADObjectType Type { get; set; }
        public string Tier
        {
            get => tier;
            set
            {
                tier = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Tier"));
            }
        }

        public string Iconpath { get; set; }
        public ICommand TierUpCommand { get; set; }
        public ICommand TierDownCommand { get; set; }
        public Dictionary<string, ADObject> Children { get; set; }
        public List<ADObject> ChildrenList => Children.Values.ToList();
        public List<ADObject> ChildrenListSorted => Children.Values.OrderBy(c => c.Type.ToString().Length).ThenBy(c => c.CN).ToList();

        public event PropertyChangedEventHandler PropertyChanged;

        private async void TierUp()
        {
            try
            {
                oUStructurePage.EnableGUIWait();
                string newTier = (Int32.Parse(Tier) + 1).ToString();
                Tier = newTier;
                await oUStructurePage.SetTier(Objectid, newTier);
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                oUStructurePage.DisableGUIWait();
                MainWindow.BackToConnectPage();
            }
            oUStructurePage.DisableGUIWait();
        }
        private async void TierDown()
        {
            try
            {
                oUStructurePage.EnableGUIWait();
                if (Tier != "0")
                {
                    string newTier = (Int32.Parse(Tier) - 1).ToString();
                    Tier = newTier;
                    await oUStructurePage.SetTier(Objectid, newTier);
                }
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                oUStructurePage.DisableGUIWait();
                MainWindow.BackToConnectPage();
            }
            oUStructurePage.DisableGUIWait();
        }

        public List<ADObject> GetAllChildren()
        {
            List<ADObject> children = ChildrenList;
            foreach (ADObject child in ChildrenList)
            {
                children.AddRange(child.GetAllChildren());
            }
            return children;
        }

        public void SetAllChildrenToTier()
        {
            GetAllChildren().ForEach(child => child.Tier = Tier);
        }
    }

    public enum ADObjectType
    {
        Unknown, Domain, Container, OU, Group, User, Computer, GPO
    }
}
