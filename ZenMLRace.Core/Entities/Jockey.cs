namespace ZenMLRace.Core.Entities;

public class Jockey
{
    public required string Id { get; set; }
    public required string Name { get; set; }

    // Navigation Property
    public ICollection<RaceEntry> RaceEntries { get; set; } = [];
}
