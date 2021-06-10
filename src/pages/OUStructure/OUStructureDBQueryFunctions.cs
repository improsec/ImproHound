using Neo4j.Driver;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImproHound.classes;

namespace ImproHound.pages
{
    public partial class OUStructurePage
    {
        private async Task PrepareDB()
        {
            try
            {
                // Fix domains without distinguishedname and domain property
                // Happens if you upload BloodHound data in the 'wrong' order
                List<IRecord> records = await DBConnection.Query(@"
                MATCH (n:Domain)
                WHERE NOT EXISTS (n.distinguishedname)
                RETURN n.name
            ");

                foreach (IRecord record in records)
                {
                    record.Values.TryGetValue("n.name", out object name);
                    string distinguishedname = "DC=" + ((string)name).ToLower().Replace(".", ",DC=");

                    await DBConnection.Query(@"
                    MATCH (n:Domain {name:'" + (string)name + @"'})
                    SET n.distinguishedname = '" + distinguishedname + @"', n.domain = n.name, n.highvalue = true
                    RETURN NULL
                ");
                }

                // Set name and distinguishedname for objects without
                List<IRecord> records1 = await DBConnection.Query(@"
                MATCH (o) WHERE NOT EXISTS(o.distinguishedname)
                MATCH (d:Domain) WHERE o.objectid STARTS WITH d.domain
                RETURN o.objectid, o.name, d.domain, d.distinguishedname
            ");

                foreach (IRecord record in records1)
                {
                    record.Values.TryGetValue("o.objectid", out object objectid);
                    record.Values.TryGetValue("o.name", out object name);
                    record.Values.TryGetValue("d.domain", out object domain);
                    record.Values.TryGetValue("d.distinguishedname", out object domainDistinguishedname);

                    string objectidStr = (string)objectid;
                    string domainStr = (string)domain;
                    string cn = objectidStr;

                    // Check if the SID match a well known one
                    IEnumerable<WellKnownADObject> matchingObj = DefaultTieringConstants.WellKnownADObjects.Where(o => o.SidEndsWith != null && objectidStr.EndsWith(o.SidEndsWith));
                    if (matchingObj.Count() > 0)
                    {
                        cn = matchingObj.FirstOrDefault().Name;

                        if (name == null)
                        {
                            name = (cn + "@" + domainStr).ToUpper();
                        }
                    }
                    // Use the name as CN if name is present
                    else if (name != null && name.ToString().Contains("@"))
                    {
                        cn = name.ToString().Substring(0, name.ToString().IndexOf("@"));
                    }
                    else if (name != null)
                    {
                        cn = name.ToString();
                    }
                    else
                    {
                        name = objectidStr + "@" + domainStr;
                    }

                    string distinguishedname = "CN=" + cn + "," + domainDistinguishedname.ToString();

                    await DBConnection.Query(@"
                    MATCH (o {objectid:'" + objectidStr + @"'})
                    SET o.name = '" + name.ToString() + @"'
                    SET o.distinguishedname = '" + distinguishedname + @"'
                    SET o.domain = '" + domainStr + @"'
                ");
                }

                // Make sure all objects do not have more than the Base label and the type of object label
                // I have seen service accounts in the BloodHound DB having both User and Computer label
                await DBConnection.Query(@"
                MATCH (n) WHERE SIZE(LABELS(n)) > 2
                UNWIND LABELS(n) AS lbls
                WITH n, lbls ORDER BY lbls ASC
                WITH n, COLLECT(lbls) AS lblsSort
                CALL apoc.create.setLabels(n, lblsSort[0..2]) YIELD node
                RETURN NULL
            ");
            }
            catch
            {
                throw;
            }
        }

        private async Task SetDefaultTiers()
        {
            try
            {
                // Well known objects with static SIDs
                IOrderedEnumerable<string> defaultTiers = DefaultTieringConstants.WellKnownADObjects.Where(o => o.SidEndsWith != null).Select(o => o.Tier).Distinct().OrderByDescending(o => o);
                int shortestSIDEnding = DefaultTieringConstants.WellKnownADObjects.Where(o => o.SidEndsWith != null).OrderBy(o => o.SidEndsWith.Length).First().SidEndsWith.Length;
                int longestSIDEnding = DefaultTieringConstants.WellKnownADObjects.Where(o => o.SidEndsWith != null).OrderByDescending(o => o.SidEndsWith.Length).First().SidEndsWith.Length;

                foreach (string tier in defaultTiers)
                {
                    IEnumerable<string> wellKnownSIDEndings = DefaultTieringConstants.WellKnownADObjects.Where(o => o.SidEndsWith != null && o.Tier.Equals(tier)).Select(o => o.SidEndsWith);

                    for (int i = shortestSIDEnding; i <= longestSIDEnding; i++)
                    {
                        if (!wellKnownSIDEndings.Count().Equals(0) && !wellKnownSIDEndings.Where(o => o.Length.Equals(i)).Count().Equals(0))
                        {
                            await DBConnection.Query(@"
                            MATCH (obj1) WHERE EXISTS(obj1.distinguishedname)
                            AND size(obj1.objectid) >= " + i + @"
                            AND substring(obj1.objectid, size(obj1.objectid) - " + i + ", " + i + ") IN ['" + String.Join("','", wellKnownSIDEndings.Where(o => o.Length.Equals(i))) + @"']
                            CALL apoc.create.addLabels(obj1, ['Tier" + tier + @"']) YIELD node
                            WITH obj1
                            MATCH (obj2)-[:MemberOf*1..]->(obj1) WHERE EXISTS(obj2.distinguishedname)
                            CALL apoc.create.addLabels(obj2, ['Tier" + tier + @"']) YIELD node
                            RETURN NULL
                        ");
                        }
                    }
                }

                // Well known objects with non-static SIDs
                IEnumerable<WellKnownADObject> wellKnownObjects = DefaultTieringConstants.WellKnownADObjects.Where(o => o.SidEndsWith == null);

                foreach (WellKnownADObject wellKnownObject in wellKnownObjects)
                {
                    await DBConnection.Query(@"
                    MATCH (obj1) WHERE EXISTS(obj1.distinguishedname)
                    AND obj1.distinguishedname STARTS WITH 'CN=" + wellKnownObject.Name + @"'
                    CALL apoc.create.addLabels(obj1, ['Tier" + wellKnownObject.Tier + @"']) YIELD node
                    WITH obj1
                    MATCH (obj2)-[:MemberOf*1..]->(obj1) WHERE EXISTS(obj2.distinguishedname)
                    CALL apoc.create.addLabels(obj2, ['Tier" + wellKnownObject.Tier + @"']) YIELD node
                    RETURN NULL
                ");
                }

                // Set OUs to be in the same tier as the lowest tier of their content
                await DBConnection.Query(@"
                MATCH (ou:OU) WHERE EXISTS(ou.distinguishedname)
                MATCH (ou)-[:Contains*1..]->(obj)
                UNWIND labels(obj) AS allLabels
                WITH DISTINCT allLabels, ou WHERE allLabels STARTS WITH 'Tier'
                WITH ou, allLabels ORDER BY allLabels ASC
                WITH ou, head(collect(allLabels)) AS rightTier
                CALL apoc.create.setLabels(ou, ['Base', 'OU', rightTier]) YIELD node
                RETURN NULL
            ");

                // Set domains to Tier 0
                await DBConnection.Query(@"
                MATCH (domain:Domain) WHERE EXISTS(domain.distinguishedname)
                CALL apoc.create.addLabels(domain, ['Tier0']) YIELD node
                RETURN NULL
            ");

                // Set GPOs to be in the same tier as the lowest tier of the OUs (or domain) linked to
                await DBConnection.Query(@"
                MATCH (gpo:GPO) WHERE EXISTS(gpo.distinguishedname)
                MATCH (gpo)-[:GpLink]->(ou)
                UNWIND labels(ou) AS allLabels
                WITH DISTINCT allLabels, gpo WHERE allLabels STARTS WITH 'Tier'
                WITH gpo, allLabels ORDER BY allLabels ASC
                WITH gpo, head(collect(allLabels)) AS rightTier
                CALL apoc.create.setLabels(gpo, ['Base', 'GPO', rightTier]) YIELD node
                RETURN NULL
            ");

                // Set all objects without tier label to default tier
                await DBConnection.Query(@"
                MATCH (o) WHERE EXISTS(o.distinguishedname)
                AND NOT ('Tier0' IN labels(o) OR 'Tier1' IN labels(o) OR 'Tier2' IN labels(o))
                UNWIND labels(o) AS allLabels
                WITH o, COLLECT(allLabels) + 'Tier" + DefaultTieringConstants.DefaultTierNumber + @"' AS newLabels
                CALL apoc.create.setLabels(o, newLabels) YIELD node
                RETURN NULL
            ");

                // Delete higher tier labels for objects in multiple tiers
                await DBConnection.Query(@"
                MATCH (n)
                UNWIND labels(n) AS label
                WITH n, label WHERE label STARTS WITH 'Tier'
                WITH n, label ORDER BY label ASC
                WITH n, tail(collect(label)) AS wrongTiers
                CALL apoc.create.removeLabels(n, wrongTiers) YIELD node
                RETURN NULL
            ");
            }
            catch
            {
                throw;
            }
        }

        private async void CreateADObjectInDB(string objectid, ADObjectType adType, string name, string distinguishedname, string domain, string tier)
        {
            try
            {
                await DBConnection.Query(@"
                    CREATE (o {objectid:'" + objectid + "', domain:'" + domain + "', distinguishedname:'" + distinguishedname + "', name:'" + name +
                    @"', improhoundcreated: true})
                    WITH o
                    CALL apoc.create.setLabels(o, ['Base', '" + adType.ToString() + "', 'Tier" + tier + @"']) YIELD node
                    RETURN NULL
                ");
            }
            catch
            {
                throw;
            }
        }

        internal async Task SetTier(string objectid, string newTier)
        {
            try
            {
                if (!ouStructureSaved) return;

                // Set new tier label for object in DB
                await DBConnection.Query(@"
                    MATCH (o {objectid:'" + objectid + @"'})
                    UNWIND labels(o) AS allLabels
                    WITH DISTINCT allLabels, o WHERE NOT allLabels STARTS WITH 'Tier'
                    WITH o, COLLECT(allLabels) + 'Tier" + newTier + @"' AS newLabels
                    CALL apoc.create.setLabels(o, newLabels) YIELD node
                    RETURN NULL
                ");
            } catch
            {
                throw;
            }
        }

        private async Task DeleteTieringInDB()
        {
            try
            {
                await DBConnection.Query(@"
                    CALL db.labels()
                    YIELD label WHERE label STARTS WITH 'Tier'
                    WITH COLLECT(label) AS labels
                    MATCH (n)
                    WITH COLLECT(n) AS nodes, labels
                    CALL apoc.create.removeLabels(nodes, labels)
                    YIELD node RETURN NULL
                ");
            }
            catch
            {
                throw;
            }
        }

        private async Task SetTieringInDB()
        {
            try
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
                await DBConnection.Query(@"
                    MATCH(obj) WHERE EXISTS(obj.distinguishedname)
                    CALL apoc.create.addLabels(obj, ['Tier' + " + largestTier.Key + @"]) YIELD node
                    RETURN NULL
                ");

                // Replace tier label for AD object not in largest tier to the right tier
                foreach (var tier in sortedTierDict)
                {
                    List<string> tierObjectIds = tier.Value.Select(obj => obj.Objectid).ToList();
                    string tierObjectIdsString = "['" + String.Join("','", tierObjectIds) + "']";
                    await DBConnection.Query(@"
                        MATCH (n:Tier" + largestTier.Key + @")
                        WHERE n.objectid IN " + tierObjectIdsString + @"
                        WITH COLLECT(n) AS nList
                        CALL apoc.refactor.rename.label('Tier' + " + largestTier.Key + ", 'Tier' + " + tier.Key + @", nList) YIELD indexes
                        RETURN NULL
                    ");
                }
            }
            catch
            {
                throw;
            }
        }
    }
}
