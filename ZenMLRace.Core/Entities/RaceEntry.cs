namespace ZenMLRace.Core.Entities;

public class RaceEntry
{
    public string RaceId { get; set; } = default!;
    public Race Race { get; set; } = default!;

    public string HorseId { get; set; } = default!;
    public Horse Horse { get; set; } = default!;

    public string JockeyId { get; set; } = default!;
    public Jockey Jockey { get; set; } = default!;

    public int BracketNumber { get; set; } // 枠番
    public int HorseNumber { get; set; } // 馬番

    public double BurdenWeight { get; set; } // 斤量

    // 以下はレース確定後、またはオッズ発表後に判明するデータ (null許容)
    public int? FinishOrder { get; set; } // 着順
    public TimeSpan? FinishTime { get; set; } // タイム
    public double? Odds { get; set; } // 単勝オッズ
    public int? Popularity { get; set; } // 人気
}
