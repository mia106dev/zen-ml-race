using ZenMLRace.Lightweight.Contracts;
using ZenMLRace.Lightweight.Services;

namespace ZenMLRace.Lightweight.Tests;

public class RuleBasedHorseScorerTest
{
    private static readonly ScoringProfile Scoring = new(
        FrameScores:
        [
            new NumericBandScore(1, 2, -0.30),
            new NumericBandScore(3, 8, 0.10)
        ],
        AgeScores:
        [
            new NumericBandScore(4, 4, 0.50),
            new NumericBandScore(5, 5, 0.80)
        ],
        PreferredRaceNameScores: new Dictionary<string, double>
        {
            ["香港スプリント"] = 0.70,
            ["安田記念"] = 0.90
        },
        LastFinishScores:
        [
            new NumericBandScore(1, 1, 1.00),
            new NumericBandScore(2, 3, 0.50),
            new NumericBandScore(4, null, -0.30)
        ],
        LastPopularityScores:
        [
            new NumericBandScore(1, 1, 0.80),
            new NumericBandScore(2, 5, 0.20),
            new NumericBandScore(6, null, -0.40)
        ]);

    private static readonly WeightProfile NeutralWeights = new(null, null, null, null);

    private static readonly RuleBasedHorseScorer Sut = new();

    private static RaceCardEntry BuildEntry(
        int horseNumber,
        int frameNumber,
        string name,
        int? age,
        IReadOnlyList<RaceCardPastRun>? pastRuns = null) =>
        new(frameNumber, horseNumber, name, "牡", age, null, null, null, pastRuns ?? []);

    private static NormalizedRaceData BuildData(params RaceCardEntry[] entries)
    {
        var horses = entries.Select(entry =>
        {
            var first = entry.PastRuns.OrderBy(static x => x.Index).FirstOrDefault();
            return new HorseProfile(
                entry.FrameNumber,
                entry.HorseNumber,
                entry.HorseName,
                entry.Age,
                first?.RaceName,
                first?.FinishPosition ?? 0,
                first?.Popularity ?? 0);
        }).ToArray();

        var raceCard = new RaceCard(
            new RaceCardRaceInfo("テストレース", null, null, null, null, null, null, null),
            entries);

        return new NormalizedRaceData("テストレース", horses, [], string.Empty, string.Empty, raceCard);
    }

    [Fact]
    public void Score_DebutHorseWithoutPastRuns_HasNoPastRaceOrPopularityReasons()
    {
        var entry = BuildEntry(1, 3, "デビュー馬", 3);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        Assert.DoesNotContain(horse.Reasons, static r => r.StartsWith("前走系評価", StringComparison.Ordinal));
        Assert.DoesNotContain(horse.Reasons, static r => r.StartsWith("人気評価", StringComparison.Ordinal));
        Assert.Equal(50.10, horse.Score, 2);
    }

    [Fact]
    public void Score_ExactPreferredRaceNameMatch_AddsMatchedBonus()
    {
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "香港スプリント", null, null) };
        var entry = BuildEntry(1, 5, "得意レース馬", null, pastRuns);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        // frame band[3,8]=0.10 + previous race (0.70 + 0.12 bonus = 0.82)
        Assert.Equal(50.92, horse.Score, 2);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("前走系評価 +0.82", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_FuzzyPreferredRaceNameMatch_StillAppliesBonus()
    {
        // 表記ゆれ（開催名などの付記）でも部分一致で得意レース扱いになること。
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "安田記念(G1)", null, null) };
        var entry = BuildEntry(1, 5, "表記ゆれ馬", null, pastRuns);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("前走系評価 +1.00", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_UnknownRaceName_AppliesUnmatchedPenalty()
    {
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "地方の平場戦", null, null) };
        var entry = BuildEntry(1, 5, "無名レース馬", null, pastRuns);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("前走系評価 -0.06", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_StrongFinishInPreferredRace_ClampsPreviousRaceRawToUnitRange()
    {
        // 得意レース(+0.82)かつ1着(+1.00*1.15=1.15)が単走で乗ると素の合計は2.15になり得るが、
        // 他要素と同様に[-1,1]へ丸め込まれること（回帰防止）。
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "香港スプリント", 1, null) };
        var entry = BuildEntry(1, 5, "圧勝馬", null, pastRuns);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        // frame band[3,8]=0.10 + previous race clamped to +1.00
        Assert.Equal(51.10, horse.Score, 2);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("前走系評価 +1.00", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_MultiplePastRuns_WeightsRecentRunMoreThanOlderRun()
    {
        var pastRuns = new[]
        {
            new RaceCardPastRun(1, null, null, "安田記念", null, null), // decay 1.00, score 0.90+0.12=1.00(clamped)
            new RaceCardPastRun(2, null, null, "地方の平場戦", null, null) // decay 0.70, score -0.06
        };
        var entry = BuildEntry(1, 5, "複数走馬", null, pastRuns);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        // decay-weighted average: (1.00*1.00 + (-0.06)*0.70) / (1.00+0.70) = 0.9560/1.70 ≒ 0.5624 -> +0.56
        Assert.Contains(horse.Reasons, static r => r.StartsWith("前走系評価 +0.56", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_PopularitySignal_IsIncludedOnlyWhenPopularityDataExists()
    {
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "地方の平場戦", 1, 1) };
        var entry = BuildEntry(1, 5, "人気馬", null, pastRuns);
        var data = BuildData(entry);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("人気評価", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_AppliesWeightMultiplier_WhenProfileSpecifiesNonNeutralWeight()
    {
        var entry = BuildEntry(1, 3, "重み付き馬", null);
        var data = BuildData(entry);
        var weights = new WeightProfile(null, null, FrameWeight: 2.0, null);

        var result = Sut.Score(data, weights, Scoring);

        var horse = Assert.Single(result);
        // frame band[3,8]=0.10 * weight 2.0
        Assert.Equal(50.20, horse.Score, 2);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("枠番評価 +0.20 (w=2)", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_OrdersResultsDescendingByScore()
    {
        var weak = BuildEntry(1, 1, "不利枠馬", null);
        var strong = BuildEntry(2, 5, "有利枠馬", null);
        var data = BuildData(weak, strong);

        var result = Sut.Score(data, NeutralWeights, Scoring);

        Assert.Equal("有利枠馬", result[0].Name);
        Assert.Equal("不利枠馬", result[1].Name);
    }
}
