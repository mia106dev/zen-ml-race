namespace ZenMLRace.Core.Entities;

public class RaceEntry
{
    public required string RaceId { get; set; }
    public required Race Race { get; set; }

    public required string HorseId { get; set; }
    public required Horse Horse { get; set; }

    public required string JockeyId { get; set; }
    public required Jockey Jockey { get; set; }

    /// <summary>
    /// 枠番
    /// </summary>
    public int BracketNumber { get; set; }
    /// <summary>
    /// 馬番
    /// </summary>
    public int HorseNumber { get; set; }
    /// <summary>
    /// 斤量
    /// </summary>
    public double BurdenWeight { get; set; }
    /// <summary>
    /// 馬体重
    /// </summary>
    public double HorseWeight { get; set; }

    // 以下はレース確定後、またはオッズ発表後に判明するデータ (null許容)
    /// <summary>
    /// 着順
    /// </summary>
    public int? FinishOrder { get; set; }
    /// <summary>
    /// タイム
    /// </summary>
    public TimeSpan? FinishTime { get; set; }
    /// <summary>
    /// 単勝オッズ
    /// </summary>
    public double? Odds { get; set; }
    /// <summary>
    /// 人気
    /// </summary>
    public int? Popularity { get; set; }
}
