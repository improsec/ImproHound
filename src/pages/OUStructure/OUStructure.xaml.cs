using Neo4j.Driver;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using ImproHound.classes;
using System.Text.RegularExpressions;
using System.Collections;

namespace ImproHound.pages
{
    public partial class OUStructurePage : Page
    {
        private readonly DBAction dBAction;
        private readonly int defaultTierNumber = 1;

        private Dictionary<string, ADObject> forest;
        private Hashtable idLookupTable;
        private bool ouStructureSaved = true;

        public OUStructurePage(DBAction dBAction)
        {
            this.dBAction = dBAction;

            InitializeComponent();
            Initialization();
        }

        private async void Initialization()
        {
            EnableGUIWait();
            idLookupTable = new Hashtable();

            if (dBAction.Equals(DBAction.StartFromScratch))
            {
                await PrepareDB();
                await SetDefaultTiers();
            }
            else if (dBAction.Equals(DBAction.StartOver))
            {
                await DeleteTieringInDB();
                await SetDefaultTiers();
            }

            await BuildOUStructure();

            if (dBAction.Equals(DBAction.StartFromScratch) || dBAction.Equals(DBAction.StartOver))
                await SetDefaultTiersForImprohoundCreatedOUs();

            DisableGUIWait();
        }

        private async Task SetDefaultTiersForImprohoundCreatedOUs()
        {
            // Update DB
            List<IRecord> records;
            try
            {
                records = await DBConnection.Query(@"
                        MATCH (ou:OU {improhoundcreated: true})
                        MATCH (n) WHERE n.distinguishedname ENDS WITH ou.distinguishedname
                        UNWIND labels(n) AS allLabels
                        WITH DISTINCT allLabels, ou WHERE allLabels STARTS WITH 'Tier'
                        WITH ou, allLabels ORDER BY allLabels ASC
                        WITH ou, head(collect(allLabels)) AS rightTier
                        CALL apoc.create.setLabels(ou, ['Base', 'OU', rightTier]) YIELD node
                        RETURN ou.objectid, rightTier
                ");
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MainWindow.BackToConnectPage();
                return;
            }

            // Update application data
            foreach (IRecord record in records)
            {
                record.Values.TryGetValue("ou.objectid", out object objectid);
                record.Values.TryGetValue("rightTier", out object tier);

                if (idLookupTable.ContainsKey((string)objectid))
                {
                    ADObject adObject = (ADObject)idLookupTable[(string)objectid];
                    adObject.Tier = ((string)tier).Replace("Tier", "");
                }
            }
        }

        private async Task BuildOUStructure()
        {
            forest = new Dictionary<string, ADObject>();

            // Create temp tier property on objects
            try
            {
                await DBConnection.Query(@"
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
                MainWindow.BackToConnectPage();
                return;
            }

            // Get all obejcts with distinguishedname (incl. objects with o.tier = null)
            List<IRecord> records;
            try
            {
                object output;
                records = await DBConnection.Query(@"
                        MATCH (o) WHERE EXISTS(o.distinguishedname)
                        UNWIND LABELS(o) AS adtype
                        WITH o.objectid AS objectid, o.name AS name, o.distinguishedname AS distinguishedname, o.tier AS tier, adtype
                        WHERE adtype IN ['Domain', 'OU', 'Group', 'User', 'Computer', 'GPO']
                        RETURN objectid, name, distinguishedname, tier, adtype ORDER BY size(distinguishedname)
                    ");
                if (!records[0].Values.TryGetValue("objectid", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    MainWindow.BackToConnectPage();
                    return;
                }
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MainWindow.BackToConnectPage();
                return;
            }

            foreach (IRecord record in records)
            {
                record.Values.TryGetValue("objectid", out object objectid);
                record.Values.TryGetValue("name", out object name);
                record.Values.TryGetValue("distinguishedname", out object distinguishedname);
                record.Values.TryGetValue("adtype", out object type);
                record.Values.TryGetValue("tier", out object tier);

                // Get tier
                string tierNumber = defaultTierNumber.ToString();
                if (tier != null) tierNumber = tier.ToString().Replace("Tier", "");

                // Get AD type
                bool gotTypeEnum = Enum.TryParse((string)type, out ADObjectType adType);
                if (!gotTypeEnum) adType = ADObjectType.Unknown;

                string distinguishednameStr = distinguishedname.ToString();

                try
                {
                    if (adType.Equals(ADObjectType.Domain))
                    {
                        ADObject adObject = new ADObject((string)objectid, adType, distinguishednameStr, (string)name, distinguishednameStr, tierNumber, this);
                        forest.Add(adObject.Distinguishedname, adObject);
                        idLookupTable.Add((string)objectid, adObject);
                    }
                    else
                    {
                        ADObject parent = GetParent(distinguishednameStr);
                        string rdnName = distinguishednameStr.Replace("," + parent.Distinguishedname, "");
                        string cn = rdnName.Substring(distinguishednameStr.IndexOf("=") + 1);
                        ADObject adObject = new ADObject((string)objectid, adType, cn, (string)name, distinguishednameStr, tierNumber, this);
                        parent.Children.Add(rdnName, adObject);
                        idLookupTable.Add((string)objectid, adObject);
                    }
                }
                catch
                {
                    Console.Error.WriteLine("Something went wrong when adding this AD object (objectid): " + objectid);
                }
            }

            // Delete temp tier property on objects
            try
            {
                await DBConnection.Query(@"
                    MATCH(o)
                    SET o.tier = NULL
                ");
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MainWindow.BackToConnectPage();
                return;
            }

            Console.WriteLine("OU Structure build");
            forestTreeView.ItemsSource = forest.Values.ToList();
        }

        private ADObject GetParent(string distinguishedname)
        {
            // Find the domain the object belongs to
            KeyValuePair<string, ADObject> domain = forest.Where(d => distinguishedname.EndsWith(d.Key)).OrderByDescending(d => d.Key.Length).First();

            if (domain.Key != null)
            {
                string[] oupath = Regex.Split(distinguishedname.Replace("," + domain.Key, ""), @"(?<!\\),");
                ADObject parent = domain.Value;
                if (oupath.Length > 1)
                {
                    for (int i = oupath.Length - 1; i > 0; i--)
                    {
                        bool parentFound = false;
                        foreach (KeyValuePair<string, ADObject> container in parent.GetOUChildren())
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
                            string containerDistinguishedname = oupath[i] + "," + parent.Distinguishedname;
                            string objectId = "container-" + containerDistinguishedname;
                            string cn = oupath[i].Replace("CN=", "");
                            string name = (cn + "@" + domain.Value.Name).ToUpper();
                            string tier = defaultTierNumber.ToString();

                            // Create as OU in application data
                            ADObject adContainer = new ADObject(objectId, ADObjectType.OU, cn, name, containerDistinguishedname, tier, this);
                            idLookupTable.Add((string)objectId, adContainer);
                            parent.Children.Add(oupath[i], adContainer);
                            parent = adContainer;

                            // Create as OU in DB
                            CreateADObjectInDB(objectId, ADObjectType.OU, name, containerDistinguishedname, domain.Value.Name, tier);
                        }
                    }
                }

                return parent;
            }

            throw new Exception("Error: Could not find ADObjects OU/Domain parent");
        }

        private void resetOUStructure()
        {
            GetAllADObjects().ForEach(obj => obj.Tier = defaultTierNumber.ToString());
        }

        private List<ADObject> GetAllADObjects()
        {
            List<ADObject> allADObjects = new List<ADObject>();
            foreach (ADObject topADObject in forest.Values)
            {
                allADObjects.Add(topADObject);
                allADObjects.AddRange(topADObject.GetAllChildren());
            }
            return allADObjects;
        }

        private void EnableGUIWait()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            mouseblock.Visibility = Visibility.Visible;
        }

        private void DisableGUIWait()
        {
            Mouse.OverrideCursor = null;
            mouseblock.Visibility = Visibility.Hidden;
        }
    }

    public enum DBAction
    {
        StartFromScratch, Continue, StartOver
    }
}
