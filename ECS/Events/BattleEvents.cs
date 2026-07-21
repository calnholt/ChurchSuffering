namespace ChurchSuffering.ECS.Events
{
    public enum BattleLocation
    {
        Desert,
        Forest,
        Cathedral,
        Tundra,
        Jungle,
        Volcano,
        TheGate,
        Gothic,
    }

    public class ChangeBattleLocationEvent
    {
        // Prefer Location; TexturePath is a manual override for non-enum assets if needed
        public BattleLocation? Location { get; set; }
        public string TexturePath { get; set; } = ""; // Content pipeline path without extension, e.g. "desert-background"
    }
}


