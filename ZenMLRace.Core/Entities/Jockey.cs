namespace ZenMLRace.Core.Entities;

public class Jockey
{
    public string Id { get; set; } = default!; // netkeibaの騎手ID
    public string Name { get; set; } = default!;

    // ナビゲーションプロパティ
    public ICollection<RaceEntry> RaceEntries { get; set; } = [];
}
