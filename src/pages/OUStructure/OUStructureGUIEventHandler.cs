using Neo4j.Driver;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.IO;
using ImproHound.classes;

namespace ImproHound.pages
{
    public partial class OUStructurePage
    {
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
                try
                {
                    if (parent.Type.Equals(ADObjectType.Domain))
                    {
                        // Delete current tier label
                        await DBConnection.Query(@"
                            CALL db.labels()
                            YIELD label WHERE label STARTS WITH 'Tier'
                            WITH COLLECT(label) AS labels
                            MATCH (n {domain:'" + parent.Name + @"'}) WHERE EXISTS(n.distinguishedname)
                            WITH COLLECT(n) AS nodes, labels
                            CALL apoc.create.removeLabels(nodes, labels) YIELD node
                            RETURN NULL
                        ");

                        // Add new tier label
                        await DBConnection.Query(@"
                            MATCH (n {domain:'" + parent.Name + @"'}) WHERE EXISTS(n.distinguishedname)
                            WITH COLLECT(n) AS nodes
                            CALL apoc.create.addLabels(nodes, ['Tier" + parent.Tier + @"']) YIELD node
                            RETURN NULL
                        ");
                    }
                    else
                    {
                        // Delete current tier label
                        await DBConnection.Query(@"
                            CALL db.labels()
                            YIELD label WHERE label STARTS WITH 'Tier'
                            WITH COLLECT(label) AS labels
                            MATCH (n) WHERE n.distinguishedname ENDS WITH '," + parent.Distinguishedname + @"'
                            WITH COLLECT(n) AS nodes, labels
                            CALL apoc.create.removeLabels(nodes, labels) YIELD node
                            RETURN NULL
                        ");

                        // Add new tier label
                        await DBConnection.Query(@"
                            MATCH (n) WHERE n.distinguishedname ENDS WITH '," + parent.Distinguishedname + @"'
                            WITH COLLECT(n) AS nodes
                            CALL apoc.create.addLabels(nodes, ['Tier" + parent.Tier + @"']) YIELD node
                            RETURN NULL
                        ");
                    }
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

        private async void setMembersButton_Click(object sender, RoutedEventArgs e)
        {
            if (forestTreeView.SelectedItem == null) return;

            EnableGUIWait();

            ADObject group = (ADObject)forestTreeView.SelectedItem;

            if (!ouStructureSaved)
            {
                await DeleteTieringInDB();
                await SetTieringInDB();
                ouStructureSaved = true;
            }

            // Update DB
            List<IRecord> records;
            try
            {
                records = await DBConnection.Query(@"
                    MATCH(o)-[:MemberOf*1..]->(group:Group {objectid:'" + group.Objectid + @"'}) WHERE EXISTS(o.distinguishedname)
                    UNWIND labels(o) AS tierlabel
                    WITH o, tierlabel
                    WHERE tierlabel STARTS WITH 'Tier'
                    CALL apoc.create.removeLabels(o, [tierlabel]) YIELD node
                    WITH o
                    CALL apoc.create.addLabels(o, ['Tier" + group.Tier + @"']) YIELD node
                    RETURN o.objectid
                ");
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
                record.Values.TryGetValue("o.objectid", out object objectid);

                ADObject adObject = (ADObject)idLookupTable[(string)objectid];
                adObject.SetTier(group.Tier);
            }

            forestTreeView.Focus();
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
                records = await DBConnection.Query(@"
                    MATCH(gpo: GPO)
                    MATCH(gpo) -[:GpLink]->(ou)
                    UNWIND labels(ou) AS allLabels
                    WITH DISTINCT allLabels, gpo WHERE allLabels STARTS WITH 'Tier'
                    WITH gpo, allLabels ORDER BY allLabels ASC
                    WITH gpo, head(collect(allLabels)) AS lowestTier
                    CALL apoc.create.setLabels(gpo, ['Base', 'GPO', lowestTier]) YIELD node
                    RETURN gpo.objectid, lowestTier
                ");
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
                record.Values.TryGetValue("gpo.objectid", out object objectid);
                record.Values.TryGetValue("lowestTier", out object lowestTier);

                ADObject gpo = (ADObject)idLookupTable[(string)objectid];
                gpo.SetTier(lowestTier.ToString().Replace("Tier", ""));
            }

            forestTreeView.Focus();
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
                    records = await DBConnection.Query(@"
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
            setChildrenButton.IsEnabled = (forestTreeView.SelectedItem != null) &&
                (((ADObject)forestTreeView.SelectedItem).Type.Equals(ADObjectType.Domain) || ((ADObject)forestTreeView.SelectedItem).Type.Equals(ADObjectType.OU));
            setMembersButton.IsEnabled = (forestTreeView.SelectedItem != null) &&
                ((ADObject)forestTreeView.SelectedItem).Type.Equals(ADObjectType.Group);
        }
    }

}
