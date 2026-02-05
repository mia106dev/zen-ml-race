using ZenMLRace.Core.Enums;

namespace ZenMLRace.Core.Entities;

public class Race
{
    public string Id { get; set; } = default!; // netkeibaのレースID (e.g. "202301010101")
    public DateTime RaceDate { get; set; }
    public string RaceName { get; set; } = default!;
    public string RacePlace { get; set; } = default!; // "東京", "中山" etc.
    public int RaceNumber { get; set; } // 第11R
    public int Distance { get; set; }
    public TrackType TrackType { get; set; }
    public Weather Weather { get; set; }
    public TrackCondition TrackCondition { get; set; }

    // ナビゲーションプロパティ
    public ICollection<RaceEntry> RaceEntries { get; set; } = [];
}
