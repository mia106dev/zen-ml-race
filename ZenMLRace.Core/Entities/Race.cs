using ZenMLRace.Core.Enums;

namespace ZenMLRace.Core.Entities;

public class Race
{
    public required string Id { get; set; }
    public DateTime RaceDate { get; set; }
    public required string RaceName { get; set; }
    public required string RaceTrack { get; set; }
    public int RaceNumber { get; set; }
    public int Distance { get; set; }
    public TrackType TrackType { get; set; }
    public Weather Weather { get; set; }
    public TrackCondition TrackCondition { get; set; }

    // Navigation Property
    public ICollection<RaceEntry> RaceEntries { get; set; } = [];
}
