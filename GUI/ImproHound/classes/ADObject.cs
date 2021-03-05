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
        public ADObject(string objectid, ADObjectType type, string cn, string distinguishedname, string tier)
        {
            Objectid = objectid;
            CN = cn;
            Distinguishedname = distinguishedname;
            Tier = tier;
            Type = type;
            Members = new Dictionary<string, ADObject>();

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
        public string Distinguishedname { get; set; }
        public ADObjectType Type { get; set; }
        public string Tier { get; set; }
        public string Iconpath { get; set; }
        public ICommand TierUpCommand { get; set; }
        public ICommand TierDownCommand { get; set; }
        public Dictionary<string, ADObject> Members { get; set; }
        public List<ADObject> MemberList => Members.Values.ToList();
        public event PropertyChangedEventHandler PropertyChanged;

        private void TierUp()
        {
            Tier = (Int32.Parse(Tier) + 1).ToString();
            PropertyChanged(this, new PropertyChangedEventArgs("Tier"));
        }
        private void TierDown()
        {
            Tier = (Math.Max((Int32.Parse(Tier) - 1), 0)).ToString();
            PropertyChanged(this, new PropertyChangedEventArgs("Tier"));
        }

        public void SetTier(string tier)
        {
            Tier = tier;
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("Tier"));
            }
        }

        public Dictionary<string, ADObject> GetOUMembers()
        {
            Dictionary<string, ADObject> ous = new Dictionary<string, ADObject>();
            foreach (KeyValuePair<string, ADObject> member in Members)
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
            List<ADObject> children = MemberList;
            foreach (ADObject child in MemberList)
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
