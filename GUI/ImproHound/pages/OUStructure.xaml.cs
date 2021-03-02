using GalaSoft.MvvmLight.Command;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ImproHound.pages
{
    public partial class OUStructurePage : Page
    {
        private readonly MainWindow containerWindow;
        private readonly DBConnection connection;
        private readonly ConnectPage connectPage;
        private Dictionary<string, ADObject> forest;
        private List<string> tiers;
        private int defaultTierNumber;

        public OUStructurePage(MainWindow containerWindow, DBConnection connection, ConnectPage connectPage, bool startover = true, int numOfTierLabels = 0)
        {
            this.containerWindow = containerWindow;
            this.connection = connection;
            this.connectPage = connectPage;
            this.defaultTierNumber = 2;

            // TODO: use numOfTierLabels and startover
            // TODO: Replace tiers variable
            tiers = new List<String>() { "0", "1", "2" };
            InitializeComponent();
            EnableGUIWait();
            BuildOUStructure(startover, numOfTierLabels);
        }

        private async void BuildOUStructure(bool startover = true, int numOfTierLabels = 0)
        {
            // TODO: Remember nodes without distinguishedname

            forest = new Dictionary<string, ADObject>();

            if (!startover)
            {
                // Create temp tier property on objects
                try
                {
                    await connection.Query(@"
                        MATCH (o)
                        UNWIND LABELS(o) AS lbls
                        WITH o, lbls WHERE lbls STARTS WITH 'Tier'
                        SET o.tier = lbls
                    ");
                }
                catch (Exception err)
                {
                    // Error
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DisableGUIWait();
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }

            // Get all obejcts with distinguishedname (incl. objects with o.tier = null)
            List<IRecord> records;
            try
            {
                object output;
                records = await connection.Query(@"
                        MATCH (o)
                        WHERE NOT o.distinguishedname IS NULL
					    UNWIND LABELS(o) AS adtype
                        WITH o.objectid AS objectid, o.distinguishedname AS distinguishedname, o.name AS name, o.tier AS tier, adtype
                        WHERE adtype IN ['Domain', 'OU', 'Group', 'User', 'Computer', 'GPO']
                        RETURN objectid, distinguishedname, name, tier, adtype ORDER BY size(distinguishedname)
                    ");
                if (!records[0].Values.TryGetValue("objectid", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DisableGUIWait();
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DisableGUIWait();
                containerWindow.NavigateToPage(connectPage);
                return;
            }

            foreach (IRecord record in records)
            {
                //TODO: Make sure same object is not comming twice, if multiple labels
                record.Values.TryGetValue("objectid", out object objectid);
                record.Values.TryGetValue("distinguishedname", out object distinguishedname);
                record.Values.TryGetValue("name", out object name);
                record.Values.TryGetValue("adtype", out object type);
                record.Values.TryGetValue("tier", out object tier);

                // Get tier
                string tierNumber = defaultTierNumber.ToString();
                if (tier != null) tierNumber = tier.ToString().Replace("Tier", "");

                // Get AD type
                bool gotTypeEnum = Enum.TryParse((string)type, out ADOjectType adType);
                if (!gotTypeEnum) adType = ADOjectType.Unknown;

                try
                {
                    if (adType.Equals(ADOjectType.Domain))
                    {
                        // TODO: Put sub domains under parent domain
                        ADObject adContainer = new ADObject((string)objectid, adType, (string)distinguishedname, (string)name, tierNumber);
                        forest.Add(adContainer.Distinguishedname, adContainer);
                    }
                    else
                    {
                        ADObject adObject = new ADObject((string)objectid, adType, (string)distinguishedname, (string)name, tierNumber);
                        ADObject parent = GetParent(adObject);
                        string rdnName = adObject.Distinguishedname.Substring(0, adObject.Distinguishedname.IndexOf(","));
                        parent.Members.Add(rdnName, adObject);
                    }
                }
                catch
                {
                    Console.Error.WriteLine("Something went wrong when adding this AD object (objectid): " + objectid);
                }
            }

            if (!startover)
            {
                // Delete temp tier property on objects
                try
                {
                    await connection.Query(@"
                        MATCH(o)
                        SET o.tier = NULL
                    ");
                }
                catch (Exception err)
                {
                    // Error
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    DisableGUIWait();
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }

            Console.WriteLine("OU Structure build");
            forestTreeView.ItemsSource = forest.Values.ToList();
            DisableGUIWait();
        }

        private ADObject GetParent(ADObject adObject)
        {
            // Find the domain the object belongs to
            // TODO: Handle if no domain / ou was not found
            // TODO: Handle non-existing containers. E.g. builtin is missing, so everything under builtin ends up directly under the domain
            foreach (KeyValuePair<string, ADObject> domain in forest)
            {
                if (adObject.Distinguishedname.EndsWith(domain.Key))
                {
                    string[] oupath = adObject.Distinguishedname.Replace("," + domain.Key, "").Split(',');
                    adObject.Name = oupath[0].Substring(oupath[0].IndexOf("=") + 1);
                    ADObject parent = domain.Value;

                    if (oupath.Length > 1)
                    {
                        for (int i = oupath.Length - 1; i > 0; i--)
                        {
                            bool parentFound = false;
                            foreach (KeyValuePair<string, ADObject> container in parent.GetOUMembers())
                            {
                                if (oupath[i].Equals(container.Key))
                                {
                                    parent = container.Value;
                                    parentFound = true;
                                    break;
                                }
                            }

                            // Containers are missing in BloodHound so they have to be created manually
                            if (!parentFound)
                            {
                                string distinguishedname = oupath[i] + "," + parent.Distinguishedname;
                                ADObject adContainer = new ADObject("manually-created-" + distinguishedname, ADOjectType.OU, distinguishedname, oupath[i].Replace("CN=", ""), "2");
                                parent.Members.Add(oupath[i], adContainer);
                                parent = adContainer;
                            }
                        }
                    }

                    return parent;
                }
            }
            throw new Exception("Error: Could not find ADObjects OU/Domain parent");
        }

        private void Set_Tiering_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            DeleteTieringInDB();
            SetTieringInDB();
        }

        private async void DeleteTieringInDB()
        {
            List<IRecord> records;
            try
            {
                object output;
                records = await connection.Query(@"
                    CALL db.labels()
                    YIELD label WHERE label STARTS WITH 'Tier'
                    WITH COLLECT(label) AS labels
                    MATCH (n)
                    WITH COLLECT(n) AS nodes, labels
                    CALL apoc.create.removeLabels(nodes, labels)
                    YIELD node RETURN null
                ");
                if (!records[0].Values.TryGetValue("null", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private async void SetTieringInDB()
        {
            // Get all AD objects
            List<ADObject> allADObjects = new List<ADObject>();
            foreach (ADObject topADObject in forest.Values)
            {
                allADObjects.Add(topADObject);
                allADObjects.AddRange(topADObject.GetAllChildren());
            }

            // Devide AD objects in tiers
            Dictionary<string, List<ADObject>> tierDict = new Dictionary<string, List<ADObject>>();
            foreach (string tier in tiers)
            {
                List<ADObject> tierADObjects = allADObjects.Where(obj => obj.Tier == tier).ToList();

                // Skip empty tiers
                if (!tierADObjects.Count().Equals(0))
                {
                    tierDict.Add(tier, tierADObjects);
                }
            }

            // Sort tiers by size
            List<KeyValuePair<string, List<ADObject>>> sortedTierDict = (from entry in tierDict orderby entry.Value.Count descending select entry).ToList();

            // Set all AD object in DB to largest tier
            KeyValuePair<string, List<ADObject>> largestTier = sortedTierDict.First();
            sortedTierDict.RemoveAt(0);
            List<IRecord> records;
            try
            {
                object output;
                string query = @"
                        MATCH(obj)
                        CALL apoc.create.addLabels(obj, ['Tier' + " + largestTier.Key + @"]) YIELD node
                        RETURN null
                    ";
                records = await connection.Query(query, false);
                if (!records[0].Values.TryGetValue("null", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DisableGUIWait();
                    return;
                }
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DisableGUIWait();
                return;
            }

            // Replace tier label for AD object not in largest tier to the right tier
            foreach (var tier in sortedTierDict)
            {
                List<string> tierObjectIds = tier.Value.Select(obj => obj.Objectid).ToList();
                string tierObjectIdsString = "['" + String.Join("','", tierObjectIds) + "']";
                try
                {
                    object output;
                    string query = @"
                        MATCH (n:Tier" + largestTier.Key + @")
                        WHERE n.objectid IN " + tierObjectIdsString + @"
                        WITH COLLECT(n) AS nList
                        CALL apoc.refactor.rename.label('Tier' + " + largestTier.Key + ", 'Tier' + " + tier.Key + @", nList) YIELD indexes
                        RETURN null
                    ";
                    records = await connection.Query(query, false);
                    if (!records[0].Values.TryGetValue("null", out output))
                    {
                        // Unknown error
                        MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        DisableGUIWait();
                        return;
                    }
                }
                catch (Exception err)
                {
                    // Error
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DisableGUIWait();
                    return;
                }
            }

            DisableGUIWait();
        }

        private void EnableGUIWait()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            setTieringButton.IsEnabled = false;
        }

        private void DisableGUIWait()
        {
            Mouse.OverrideCursor = null;
            setTieringButton.IsEnabled = true;
        }
    }

    public class ADObject : INotifyPropertyChanged
    {
        public ADObject(string objectid, ADOjectType type, string distinguishedname, string name, string tier)
        {
            Objectid = objectid;
            Name = name;
            Distinguishedname = distinguishedname;
            Tier = tier;
            Type = type;
            Members = new Dictionary<string, ADObject>();

            TierUpCommand = new RelayCommand(TierUp);
            TierDownCommand = new RelayCommand(TierDown);

            switch (type)
            {
                case ADOjectType.Domain:
                    Iconpath = "/resources/images/ad-icons/domain1.png";
                    break;
                case ADOjectType.OU:
                    Iconpath = "/resources/images/ad-icons/ou.png";
                    break;
                case ADOjectType.Group:
                    Iconpath = "/resources/images/ad-icons/group.png";
                    break;
                case ADOjectType.User:
                    Iconpath = "/resources/images/ad-icons/user.png";
                    break;
                case ADOjectType.Computer:
                    Iconpath = "/resources/images/ad-icons/computer.png";
                    break;
                case ADOjectType.GPO:
                    Iconpath = "/resources/images/ad-icons/gpo.png";
                    break;
                default:
                    Iconpath = "/resources/images/ad-icons/domain2.png";
                    break;
            }
        }
        public string Objectid { get; set; }
        public string Name { get; set; }
        public string Distinguishedname { get; set; }
        public ADOjectType Type { get; set; }
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

        public Dictionary<string, ADObject> GetOUMembers()
        {
            Dictionary<string, ADObject> ous = new Dictionary<string, ADObject>();
            foreach (KeyValuePair<string, ADObject> member in Members)
            {
                if (member.Value.Type is ADOjectType.OU)
                {
                    ous.Add(member.Key, member.Value);
                }
            }
            return ous;
        }

        public List<ADObject> GetAllChildren()
        {
            List<ADObject> children = Members.Values.ToList();
            foreach (ADObject child in Members.Values.ToList())
            {
                children.AddRange(child.GetAllChildren());
            }
            return children;
        }
    }

    public enum ADOjectType
    {
        Unknown, Domain, OU, Group, User, Computer, GPO
    }
}
