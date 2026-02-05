using ZenMLRace.Core.Enums;

namespace ZenMLRace.Core.Entities;

public class Horse
{
    public string Id { get; set; } = default!; // netkeibaの馬ID
    public string Name { get; set; } = default!;
    public DateTime? BirthDate { get; set; }
    public HorseSex Sex { get; set; }
    public HorseCoatColor CoatColor { get; set; }

    // ナビゲーションプロパティ
    public ICollection<RaceEntry> RaceEntries { get; set; } = [];
}
