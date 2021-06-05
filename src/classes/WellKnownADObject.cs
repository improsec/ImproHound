namespace ImproHound
{
    public class WellKnownADObject
    {
        public WellKnownADObject(string sidEndsWith, string name, string tier)
        {
            SidEndsWith = sidEndsWith;
            Name = name;
            Tier = tier;
        }

        public string SidEndsWith { get; }
        public string Name { get; }
        public string Tier { get; }
    }
}
