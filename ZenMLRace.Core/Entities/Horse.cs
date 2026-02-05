using ZenMLRace.Core.Enums;

namespace ZenMLRace.Core.Entities;

public class Horse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required int Age { get; set; }
    public HorseSex Sex { get; set; }
    public HorseCoatColor CoatColor { get; set; }
    public required Horse Father { get; set; }
    public required Horse Mother { get; set; }

    // Navigation Property
    public ICollection<RaceEntry> RaceEntries { get; set; } = [];
}
