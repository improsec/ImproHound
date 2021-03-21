using Neo4j.Driver;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.IO;
using ImproHound.classes;

namespace ImproHound.pages
{
    public partial class OUStructurePage : Page
    {
        private readonly MainWindow containerWindow;
        private readonly DBConnection connection;
        private readonly ConnectPage connectPage;
        private Dictionary<string, ADObject> forest;
        private readonly int defaultTierNumber = 1;
        private bool ouStructureSaved = false;

        public OUStructurePage(MainWindow containerWindow, DBConnection connection, ConnectPage connectPage, bool alreadyTiered, bool startover)
        {
            this.containerWindow = containerWindow;
            this.connection = connection;
            this.connectPage = connectPage;

            InitializeComponent();
            Initialization(alreadyTiered, startover);
        }

        private async void Initialization(bool alreadyTiered, bool startover)
        {
            EnableGUIWait();
            ouStructureSaved = alreadyTiered && !startover;
            if (startover) await DeleteTieringInDB();
            await BuildOUStructure(alreadyTiered, startover);
            DisableGUIWait();
        }

        private async Task BuildOUStructure(bool alreadyTiered, bool startover)
        {
            forest = new Dictionary<string, ADObject>();

            if (!alreadyTiered)
            {
                // Make sure all objects do not have more than the Base label and the type of object label
                // I have seen service accounts in the BloodHound DB having both User and Computer label
                try
                {
                    await connection.Query(@"
                        MATCH (n) WHERE SIZE(LABELS(n)) > 2
                        UNWIND LABELS(n) AS lbls
                        WITH n, lbls ORDER BY lbls ASC
                        WITH n, COLLECT(lbls) AS lblsSort
                        CALL apoc.create.setLabels(n, lblsSort[0..2]) YIELD node
                        RETURN NULL
                    ");
                }
                catch (Exception err)
                {
                    // Error
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }

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
                        WITH o.objectid AS objectid, o.name AS name, o.distinguishedname AS distinguishedname, o.tier AS tier, adtype
                        WHERE adtype IN ['Domain', 'OU', 'Group', 'User', 'Computer', 'GPO']
                        RETURN objectid, name, distinguishedname, tier, adtype ORDER BY size(distinguishedname)
                    ");
                if (!records[0].Values.TryGetValue("objectid", out output))
                {
                    // Unknown error
                    MessageBox.Show("Something went wrong. Neo4j server response format is unexpected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                containerWindow.NavigateToPage(connectPage);
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

                // Get CN
                string distinguishednameStr = (string)distinguishedname;
                string cn = adType.Equals(ADObjectType.Domain)
                    ? distinguishednameStr
                    : distinguishednameStr.Substring(distinguishednameStr.IndexOf("=") + 1, distinguishednameStr.IndexOf(",") - distinguishednameStr.IndexOf("=") - 1);

                try
                {
                    ADObject adObject = new ADObject((string)objectid, adType, cn, (string)name, distinguishednameStr, tierNumber, this);

                    if (adType.Equals(ADObjectType.Domain))
                    {
                        forest.Add(adObject.Distinguishedname, adObject);
                    }
                    else
                    {
                        ADObject parent = GetParent(adObject.Distinguishedname);
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
                    containerWindow.NavigateToPage(connectPage);
                    return;
                }
            }

            Console.WriteLine("OU Structure build");
            forestTreeView.ItemsSource = forest.Values.ToList();
        }

        internal async void SetTier(string objectid, string newTier)
        {
            if (!ouStructureSaved) return;

            // Set new tier label for object in DB
            List<IRecord> records;
            try
            {
                records = await connection.Query(@"
                    MATCH (o {objectid:'" + objectid + @"'})
                    UNWIND labels(o) AS allLabels
                    WITH DISTINCT allLabels, o WHERE NOT allLabels STARTS WITH 'Tier'
                    WITH o, COLLECT(allLabels) + 'Tier" + newTier + @"' AS newLabels
                    CALL apoc.create.setLabels(o, newLabels) YIELD node
                    RETURN NULL
                ");
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                containerWindow.NavigateToPage(connectPage);
                return;
            }
        }

        private ADObject GetParent(string distinguishedname)
        {
            // Find the domain the object belongs to
            KeyValuePair<string, ADObject> domain = forest.Where(d => distinguishedname.EndsWith(d.Key)).OrderByDescending(d => d.Key.Length).First();

            if (domain.Key != null)
            {
                string[] oupath = distinguishedname.Replace("," + domain.Key, "").Split(',');
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
                            string containerDistinguishedname = oupath[i] + "," + parent.Distinguishedname;
                            string objectId = "container-" + containerDistinguishedname;
                            string cn = oupath[i].Replace("CN=", "");
                            string tier = defaultTierNumber.ToString();

                            // Create as OU in application data
                            ADObject adContainer = new ADObject(objectId, ADObjectType.OU, cn, cn, containerDistinguishedname, tier, this);
                            parent.Members.Add(oupath[i], adContainer);
                            parent = adContainer;

                            // Create as OU in DB
                            CreateADObjectInDB(objectId, ADObjectType.OU, cn, containerDistinguishedname, tier);
                        }
                    }
                }

                return parent;
            }

            throw new Exception("Error: Could not find ADObjects OU/Domain parent");
        }

        private async void CreateADObjectInDB(string objectid, ADObjectType adType, string name, string distinguishedname, string tier)
        {
            List<IRecord> records;
            try
            {
                records = await connection.Query(@"
                    CREATE (o {objectid:'" + objectid + "', distinguishedname:'" + distinguishedname + "', name:'" + name + @"'})
                    WITH o
                    CALL apoc.create.setLabels(o, ['Base', '" + adType.ToString() + "', 'Tier" + tier + @"']) YIELD node
                    RETURN NULL
                ");
            }
            catch (Exception err)
            {
                // Error
                MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private async Task DeleteTieringInDB()
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
                    YIELD node RETURN NULL
                ");
                if (!records[0].Values.TryGetValue("NULL", out output))
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

        private async Task SetTieringInDB()
        {
            // Get all AD objects
            List<ADObject> allADObjects = new List<ADObject>();
            foreach (ADObject topADObject in forest.Values)
            {
                allADObjects.Add(topADObject);
                allADObjects.AddRange(topADObject.GetAllChildren());
            }

            // Devide AD objects into tiers and sort by number of objects in tiers
            Dictionary<string, List<ADObject>> tierDict = allADObjects.GroupBy(g => g.Tier).ToDictionary(group => group.Key, group => group.ToList());
            List<KeyValuePair<string, List<ADObject>>> sortedTierDict = (from entry in tierDict orderby entry.Value.Count descending select entry).ToList();

            // Set all AD object in DB to largest tier
            KeyValuePair<string, List<ADObject>> largestTier = sortedTierDict.First();
            sortedTierDict.RemoveAt(0);
            List<IRecord> records;
            try
            {
                object output;
                string query = @"
                        MATCH(obj) WHERE EXISTS(obj.distinguishedname)
                        CALL apoc.create.addLabels(obj, ['Tier' + " + largestTier.Key + @"]) YIELD node
                        RETURN NULL
                    ";
                records = await connection.Query(query);
                if (!records[0].Values.TryGetValue("NULL", out output))
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
                        RETURN NULL
                    ";
                    records = await connection.Query(query);
                    if (!records[0].Values.TryGetValue("NULL", out output))
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
        }

        private void resetOUStructure()
        {
            GetAllADObjects().ForEach(obj => obj.SetTier(defaultTierNumber.ToString()));
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

        private bool SetChildrenButtonEnabled()
        {
            return forestTreeView.SelectedItem != null &&
                (((ADObject)forestTreeView.SelectedItem).Type.Equals(ADObjectType.Domain) || ((ADObject)forestTreeView.SelectedItem).Type.Equals(ADObjectType.OU));
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

        /// BUTTON CLICKS

        private async void resetButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            MessageBoxResult messageBoxResult = MessageBox.Show("Reset will delete tier labels in DB and set all objects in the OU structure to Tier " + defaultTierNumber.ToString(),
                "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (messageBoxResult.Equals(MessageBoxResult.OK))
            {
                ouStructureSaved = false;
                resetOUStructure();
                await DeleteTieringInDB();
            }
            DisableGUIWait();
        }

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            if (!ouStructureSaved)
            {
                await DeleteTieringInDB();
                await SetTieringInDB();
                ouStructureSaved = true;
            }
            DisableGUIWait();
        }

        private async void setChildrenButton_Click(object sender, RoutedEventArgs e)
        {
            if (forestTreeView.SelectedItem == null) return;

            EnableGUIWait();

            // Set GUI
            ADObject parent = (ADObject)forestTreeView.SelectedItem;
            parent.GetAllChildren().ForEach(child => child.SetTier(parent.Tier));
            forestTreeView.Focus();

            if (ouStructureSaved)
            {
                // Update DB
                List<IRecord> records;
                try
                {
                    records = await connection.Query(@"
                    CALL db.labels()
                    YIELD label WHERE label STARTS WITH 'Tier'
                    WITH COLLECT(label) AS labels
                    MATCH (n) WHERE n.distinguishedname ENDS WITH '," + parent.Distinguishedname + @"'
                    WITH COLLECT(n) AS nodes, labels
                    CALL apoc.create.removeLabels(nodes, labels) YIELD node
                    WITH nodes
                    CALL apoc.create.addLabels(nodes, ['Tier" + parent.Tier + @"']) YIELD node
                    RETURN NULL
                ");
                }
                catch (Exception err)
                {
                    // Error
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            DisableGUIWait();
        }

        private async void setTierGPOsButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();

            if (!ouStructureSaved)
            {
                await DeleteTieringInDB();
                await SetTieringInDB();
                ouStructureSaved = true;
            }

            // Set GPOs to the lowest tier of the GPOs they are linked to
            List<IRecord> records;
            try
            {
                object output;
                records = await connection.Query(@"
                    MATCH(gpo: GPO)
                    MATCH(gpo) -[:GpLink]->(ou)
                    UNWIND labels(ou) AS allLabels
                    WITH DISTINCT allLabels, gpo WHERE allLabels STARTS WITH 'Tier'
                    WITH gpo, allLabels ORDER BY allLabels ASC
                    WITH gpo, head(collect(allLabels)) AS lowestTier
                    CALL apoc.create.setLabels(gpo, ['Base', 'GPO', lowestTier]) YIELD node
                    RETURN gpo.distinguishedname, lowestTier
                ");
                if (!records[0].Values.TryGetValue("lowestTier", out output))
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

            // Update application data
            foreach (IRecord record in records)
            {
                record.Values.TryGetValue("gpo.distinguishedname", out object distinguishedname);
                record.Values.TryGetValue("lowestTier", out object lowestTier);

                ADObject parent = GetParent((string)distinguishedname);
                ADObject gpo = parent.MemberList.Where(member => member.Distinguishedname.Equals((string)distinguishedname)).FirstOrDefault();
                gpo.SetTier(lowestTier.ToString().Replace("Tier", ""));
            }

            DisableGUIWait();
        }

        private async void getTieringViolationsButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();

            if (!ouStructureSaved)
            {
                await DeleteTieringInDB();
                await SetTieringInDB();
                ouStructureSaved = true;
            }

            // Generate list of tier pairs (source, target)
            List<ADObject> allADObjects = GetAllADObjects();
            List<string> tiers = allADObjects.Select(o => o.Tier).Distinct().OrderByDescending(tier => tier).ToList();
            List<string[]> tierPairs = new List<string[]>();
            for (int i = 0; i < tiers.Count - 1; i++)
            {
                for (int j = i + 1; j < tiers.Count; j++)
                {
                    tierPairs.Add(new string[] { tiers.ElementAt(i), tiers.ElementAt(j) });
                }
            }

            // Prepare csv contents
            string csvHeaderADObjects = @"Tier;
                                Type;
                                Name;
                                Distinguishedname";
            string csvHeaderViolations = @"SourceTier;
                                SourceType;
                                SourceName;
                                SourceDistinguishedname;
                                Relation;
                                IsInherited;
                                TargetTier;
                                TargetType;
                                TargetName;
                                TargetDistinguishedname";
            List<string> csvADObjects = new List<string>() { String.Concat(csvHeaderADObjects.Where(c => !Char.IsWhiteSpace(c))) };
            List<string> csvViolations = new List<string>() { String.Concat(csvHeaderViolations.Where(c => !Char.IsWhiteSpace(c))) };

            // Create csv content: ADobjects
            foreach (ADObject aDObject in allADObjects)
            {
                csvADObjects.Add("Tier" + aDObject.Tier + ";" +
                    aDObject.Type + ";" +
                    aDObject.Name + ";" +
                    aDObject.Distinguishedname);
            }

            // Create csv content: Tiering violations
            foreach (string[] tierPair in tierPairs)
            {
                string sourceTier = "Tier" + tierPair[0];
                string targetTier = "Tier" + tierPair[1];

                List<IRecord> records;
                try
                {
                    records = await connection.Query(@"
                        MATCH (sourceObj:" + sourceTier + ") -[r]->(targetObj:" + targetTier + @")
                        UNWIND LABELS(sourceObj) AS sourceObjlbls
                        UNWIND LABELS(targetObj) AS targetObjlbls
                        WITH sourceObj, sourceObjlbls, targetObjlbls, r, targetObj
                        WHERE sourceObjlbls <> '" + sourceTier + @"' AND sourceObjlbls <> 'Base'
                        AND targetObjlbls <> '" + targetTier + @"' AND targetObjlbls <> 'Base'
                        RETURN sourceObjlbls, sourceObj.name, sourceObj.distinguishedname, TYPE(r), r.isinherited, targetObjlbls, targetObj.name, targetObj.distinguishedname
                    ");
                }
                catch (Exception err)
                {
                    // Error
                    MessageBox.Show(err.Message.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (IRecord record in records)
                {
                    record.Values.TryGetValue("sourceObj.name", out object sourceName);
                    record.Values.TryGetValue("sourceObj.distinguishedname", out object sourceDistinguishedname);
                    record.Values.TryGetValue("sourceObjlbls", out object sourceType);
                    record.Values.TryGetValue("TYPE(r)", out object relation);
                    record.Values.TryGetValue("r.isinherited", out object isinherited);
                    record.Values.TryGetValue("targetObj.name", out object targetName);
                    record.Values.TryGetValue("targetObj.distinguishedname", out object targetDistinguishedname);
                    record.Values.TryGetValue("targetObjlbls", out object targetType);

                    csvViolations.Add(sourceTier + ";" +
                        sourceType + ";" +
                        sourceName + ";" +
                        sourceDistinguishedname + ";" +
                        relation + ";" +
                        isinherited + ";" +
                        targetTier + ";" +
                        targetType + ";" +
                        targetName + ";" +
                        targetDistinguishedname);
                }
            }

            // Save csvs
            string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            string csvFilenameADObjects = "adobjects-" + timeStamp + ".csv";
            string csvFilenameViolations = "tiering-violations-" + timeStamp + ".csv";
            File.AppendAllLines(csvFilenameADObjects, csvADObjects);
            File.AppendAllLines(csvFilenameViolations, csvViolations);
            MessageBox.Show("ADObject list and tiering violations csvs saved in:\n\n" + Path.GetFullPath(csvFilenameADObjects) + "\n\n" + Path.GetFullPath(csvFilenameViolations),
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            DisableGUIWait();
        }

        /// OTHER GUI CHANGES

        private void forestTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            setChildrenButton.IsEnabled = SetChildrenButtonEnabled();
        }
    }
}
