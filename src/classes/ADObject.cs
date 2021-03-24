using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace ImproHound.classes
{
    public class ADObject : INotifyPropertyChanged
    {
        readonly pages.OUStructurePage oUStructurePage;

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
        public string Tier { get; set; }
        public string Iconpath { get; set; }
        public ICommand TierUpCommand { get; set; }
        public ICommand TierDownCommand { get; set; }
        public Dictionary<string, ADObject> Children { get; set; }
        public List<ADObject> ChildrenList => Children.Values.ToList();
        public List<ADObject> ChildrenListSorted => Children.Values.OrderBy(c => c.Type.ToString().Length).ThenBy(c => c.CN).ToList();

        public event PropertyChangedEventHandler PropertyChanged;

        private void TierUp()
        {
            string newTier = (Int32.Parse(Tier) + 1).ToString();
            oUStructurePage.SetTier(Objectid, newTier);
            Tier = newTier;
            PropertyChanged(this, new PropertyChangedEventArgs("Tier"));
        }
        private void TierDown()
        {
            if (Tier != "0")
            {
                string newTier = (Int32.Parse(Tier) - 1).ToString();
                oUStructurePage.SetTier(Objectid, newTier);
                Tier = newTier;
                PropertyChanged(this, new PropertyChangedEventArgs("Tier"));
            }
        }

        public void SetTier(string tier)
        {
            Tier = tier;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Tier"));
        }

        public Dictionary<string, ADObject> GetOUChildren()
        {
            Dictionary<string, ADObject> ous = new Dictionary<string, ADObject>();
            foreach (KeyValuePair<string, ADObject> member in Children)
            {
                if (member.Value.Type is ADObjectType.OU)
                {
                    ous.Add(member.Key, member.Value);
                }
            }
            return ous;
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
    }

    public enum ADObjectType
    {
        Unknown, Domain, OU, Group, User, Computer, GPO
    }
}
