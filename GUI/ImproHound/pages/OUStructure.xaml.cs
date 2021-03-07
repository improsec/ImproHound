﻿using Neo4j.Driver;
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

        public OUStructurePage(MainWindow containerWindow, DBConnection connection, ConnectPage connectPage, bool startover = true)
        {
            this.containerWindow = containerWindow;
            this.connection = connection;
            this.connectPage = connectPage;

            InitializeComponent();
            Initialization(startover);
        }

        private async void Initialization(bool startover = true)
        {
            EnableGUIWait();
            if (startover) await DeleteTieringInDB();
            await BuildOUStructure(startover);
            DisableGUIWait();
        }

        private async Task BuildOUStructure(bool startover = true)
        {
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
                        WITH o.objectid AS objectid, o.distinguishedname AS distinguishedname, o.tier AS tier, adtype
                        WHERE adtype IN ['Domain', 'OU', 'Group', 'User', 'Computer', 'GPO']
                        RETURN objectid, distinguishedname, tier, adtype ORDER BY size(distinguishedname)
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
                    ADObject adObject = new ADObject((string)objectid, adType, cn, distinguishednameStr, tierNumber);

                    if (adType.Equals(ADObjectType.Domain))
                    {
                        // TODO: Put sub domains under parent domain
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

        private ADObject GetParent(string distinguishedname)
        {
            // Find the domain the object belongs to
            foreach (KeyValuePair<string, ADObject> domain in forest)
            {
                if (distinguishedname.EndsWith(domain.Key))
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
                                ADObject adContainer = new ADObject("manually-created-" + containerDistinguishedname, ADObjectType.OU, oupath[i].Replace("CN=", ""), containerDistinguishedname, defaultTierNumber.ToString());
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
                        RETURN null
                    ";
                records = await connection.Query(query, false);
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
            resetButton.IsEnabled = false;
            saveButton.IsEnabled = false;
            setTierGPOsButton.IsEnabled = false;
            getTieringViolationsButton.IsEnabled = false;
            setChildrenButton.IsEnabled = false;
        }

        private void DisableGUIWait()
        {
            Mouse.OverrideCursor = null;
            resetButton.IsEnabled = true;
            saveButton.IsEnabled = true;
            setTierGPOsButton.IsEnabled = true;
            getTieringViolationsButton.IsEnabled = true;
            setChildrenButton.IsEnabled = SetChildrenButtonEnabled();
        }

        /// BUTTON CLICKS

        private async void resetButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            resetOUStructure();
            await DeleteTieringInDB();
            DisableGUIWait();
        }

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            await DeleteTieringInDB();
            await SetTieringInDB();
            DisableGUIWait();
        }

        private async void setChildrenButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();
            await DeleteTieringInDB();

            if (forestTreeView.SelectedItem == null) return;

            ADObject parent = (ADObject)forestTreeView.SelectedItem;
            parent.GetAllChildren().ForEach(child => child.SetTier(parent.Tier));
            forestTreeView.Focus();

            await SetTieringInDB();
            DisableGUIWait();
        }

        private async void setTierGPOsButton_Click(object sender, RoutedEventArgs e)
        {
            EnableGUIWait();

            // Make sure data is consistent
            await DeleteTieringInDB();
            await SetTieringInDB();

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

            // Make sure data is consistent
            await DeleteTieringInDB();
            await SetTieringInDB();

            // Generate list of tier pairs (source, target)
            List<string> tiers = GetAllADObjects().Select(o => o.Tier).Distinct().OrderByDescending(tier => tier).ToList();
            List<string[]> tierPairs = new List<string[]>();
            for (int i = 0; i < tiers.Count - 1; i++)
            {
                for (int j = i + 1; j < tiers.Count; j++)
                {
                    tierPairs.Add(new string[] { tiers.ElementAt(i), tiers.ElementAt(j) });
                }
            }

            // Prepare csvOutput
            string header = @"SourceTier;
                                SourceType;
                                SourceName;
                                SourceDistinguishedname;
                                Relation;
                                TargetTier;
                                TargetType;
                                TargetName;
                                TargetDistinguishedname";
            List<string> csvOutput = new List<string>() { String.Concat(header.Where(c => !Char.IsWhiteSpace(c))) };

            // Get tiering violations for each tier pair
            foreach (string[] tierPair in tierPairs)
            {
                string sourceTier = "Tier" + tierPair[0];
                string targetTier = "Tier" + tierPair[1];

                List<IRecord> records;
                try
                {
                    object output;
                    records = await connection.Query(@"
                        MATCH (sourceObj:" + sourceTier + ") -[r]->(targetObj:" + targetTier + @")
                        UNWIND LABELS(sourceObj) AS sourceObjlbls
                        UNWIND LABELS(targetObj) AS targetObjlbls
                        WITH sourceObj, sourceObjlbls, targetObjlbls, r, targetObj
                        WHERE sourceObjlbls <> '" + sourceTier + @"' AND sourceObjlbls <> 'Base'
                        AND targetObjlbls <> '" + targetTier + @"' AND targetObjlbls <> 'Base'
                        RETURN sourceObjlbls, sourceObj.name, sourceObj.distinguishedname, TYPE(r), targetObjlbls, targetObj.name, targetObj.distinguishedname
                    ");
                    if (!records[0].Values.TryGetValue("TYPE(r)", out output))
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

                foreach (IRecord record in records)
                {
                    record.Values.TryGetValue("sourceObj.name", out object sourceName);
                    record.Values.TryGetValue("sourceObj.distinguishedname", out object sourceDistinguishedname);
                    record.Values.TryGetValue("sourceObjlbls", out object sourceType);
                    record.Values.TryGetValue("TYPE(r)", out object relation);
                    record.Values.TryGetValue("targetObj.name", out object targetName);
                    record.Values.TryGetValue("targetObj.distinguishedname", out object targetDistinguishedname);
                    record.Values.TryGetValue("targetObjlbls", out object targetType);

                    csvOutput.Add(sourceTier + ";" +
                        sourceType + ";" +
                        sourceName + ";" +
                        sourceDistinguishedname + ";" +
                        relation + ";" +
                        targetTier + ";" +
                        targetType + ";" +
                        targetName + ";" +
                        targetDistinguishedname);
                }
            }

            // Save tier violations
            string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            string filename = "tiering-violations-" + timeStamp + ".csv";
            File.AppendAllLines(filename, csvOutput);
            MessageBox.Show("Tiering violations csv: " + Path.GetFullPath(filename), "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            DisableGUIWait();
        }

        /// OTHER GUI CHANGES

        private void forestTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            setChildrenButton.IsEnabled = SetChildrenButtonEnabled();
        }
    }
}
