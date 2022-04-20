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
        private const string DBCustomNodeProperty = "improhoundcreated";
        private Dictionary<string, ADObject> forest;
        private Hashtable idLookupTable;

        public OUStructurePage(DBAction dBAction)
        {
            InitializeComponent();
            Initialization(dBAction);
        }

        private async void Initialization(DBAction dBAction)
        {
            try
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
                DisableGUIWait();
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString() + "\n\n" + err.StackTrace.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DisableGUIWait();
                MainWindow.BackToConnectPage();
            }
        }

        private async Task BuildOUStructure()
        {
            try
            {
                forest = new Dictionary<string, ADObject>();

                // Create temp tier property on objects
                await DBConnection.Query(@"
                    MATCH (o)
                    UNWIND LABELS(o) AS lbls
                    WITH o, lbls WHERE lbls STARTS WITH 'Tier'
                    SET o.tier = lbls
                ");

                // Get all obejcts with distinguishedname (incl. objects with o.tier = null)
                List<IRecord> records = await DBConnection.Query(@"
                    MATCH (o) WHERE EXISTS(o.distinguishedname)
                    UNWIND LABELS(o) AS adtype
                    WITH o.objectid AS objectid, o.name AS name, o.distinguishedname AS distinguishedname, o.tier AS tier, adtype
                    WHERE adtype IN ['Domain', 'Container', 'OU', 'Group', 'User', 'Computer', 'GPO']
                    RETURN objectid, name, distinguishedname, tier, adtype ORDER BY size(distinguishedname)
                ");

                foreach (IRecord record in records)
                {
                    record.Values.TryGetValue("objectid", out object objectid);
                    record.Values.TryGetValue("name", out object name);
                    record.Values.TryGetValue("distinguishedname", out object distinguishedname);
                    record.Values.TryGetValue("adtype", out object type);
                    record.Values.TryGetValue("tier", out object tier);

                    // Get tier
                    string tierNumber = DefaultTieringConstants.DefaultTierNumber.ToString();
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
                        throw;
                    }
                }

                // Delete temp tier property on objects
                await DBConnection.Query(@"
                    MATCH(o)
                    SET o.tier = NULL
                ");

                Console.WriteLine("OU Structure build");
                forestTreeView.ItemsSource = forest.Values.ToList();
            }
            catch
            {
                throw;
            }
        }

        private ADObject GetParent(string distinguishedname)
        {
            try
            {
                // Find the domain the object belongs to
                KeyValuePair<string, ADObject> domain = forest.Where(d => distinguishedname.EndsWith(d.Key)).OrderByDescending(d => d.Key.Length).First();

                if (domain.Key != null)
                {
                    string[] oupath = Regex.Split(distinguishedname.Replace("," + domain.Key, ""), @"(?<!\\),");
                    ADObject parent = domain.Value;

                    if (oupath.Length > 1)
                    {
                        string currentDistinguishedname = parent.Distinguishedname;

                        // Iterate from domain object down to the parent object
                        for (int i = oupath.Length - 1; i > 0; i--)
                        {
                            // Get the parent to the current object
                            currentDistinguishedname = oupath[i] + "," + currentDistinguishedname;
                            if (parent.ChildrenList.Any(obj => obj.Distinguishedname.Equals(currentDistinguishedname)))
                            {
                                parent = parent.ChildrenList.Where(obj => obj.Distinguishedname.Equals(currentDistinguishedname)).First();
                            }

                            // Objects are sometimes missing in BloodHound so they have to be created manually
                            else
                            {
                                string missingDistinguishedname = currentDistinguishedname;
                                string missingObjectId = currentDistinguishedname;
                                string missingCn = oupath[i].Substring(oupath[i].IndexOf('=') + 1);
                                string missingName = (missingCn + "@" + domain.Value.Name).ToUpper();
                                string missingTier = DefaultTieringConstants.DefaultTierNumber.ToString();
                                ADObjectType missingADType = oupath[i].StartsWith("OU=") ? ADObjectType.OU : ADObjectType.Container;

                                // Create as object in application data
                                ADObject missingObject = new ADObject(missingObjectId, missingADType, missingCn, missingName, missingDistinguishedname, missingTier, this);
                                idLookupTable.Add((string)missingObjectId, missingObject);
                                parent.Children.Add(oupath[i], missingObject);
                                parent = missingObject;

                                // Create as object in DB
                                CreateADObjectInDB(missingObjectId, missingADType, missingName, missingDistinguishedname, domain.Value.Name, missingTier);
                            }
                        }
                    }

                    return parent;
                }
            }
            catch
            {
                throw;
            }

            throw new Exception("Error: Could not find ADObject's parent");
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

        public void EnableGUIWait()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            mouseblock.Visibility = Visibility.Visible;
        }

        public void DisableGUIWait()
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
