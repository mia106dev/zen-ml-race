using ZenMLRace.Lightweight.Contracts;
using ZenMLRace.Lightweight.Services;

namespace ZenMLRace.Lightweight.Tests;

public class PopularitySuppressedHorseScorerTest
{
    private static readonly ScoringProfile Scoring = new(
        FrameScores: [new NumericBandScore(3, 8, 0.10)],
        AgeScores: [],
        PreferredRaceNameScores: new Dictionary<string, double>(),
        LastFinishScores: [new NumericBandScore(1, 1, 1.00)],
        LastPopularityScores: [new NumericBandScore(1, 1, 0.80)]);

    private static readonly WeightProfile NeutralWeights = new(null, null, null, null);

    private static RaceCardEntry BuildEntry(
        int horseNumber,
        int frameNumber,
        string name,
        IReadOnlyList<RaceCardPastRun>? pastRuns = null) =>
        new(frameNumber, horseNumber, name, "牡", null, null, null, null, pastRuns ?? []);

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
    public void Score_SuppressesPopularityReasonByConfiguredFactor()
    {
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "地方の平場戦", 1, 1) };
        var entry = BuildEntry(1, 3, "人気馬", pastRuns);
        var data = BuildData(entry);
        var sut = new PopularitySuppressedHorseScorer(popularitySuppressionFactor: 0.45);

        var result = sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        Assert.Equal(51.35, horse.Score, 2);
        Assert.Contains(horse.Reasons, static r =>
            r.StartsWith("人気評価 +0.25", StringComparison.Ordinal)
            && r.EndsWith("[suppressed x0.45]", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_LeavesNonPopularityReasonsUnaffected()
    {
        var pastRuns = new[] { new RaceCardPastRun(1, null, null, "地方の平場戦", 1, 1) };
        var entry = BuildEntry(1, 3, "人気馬", pastRuns);
        var data = BuildData(entry);
        var sut = new PopularitySuppressedHorseScorer(popularitySuppressionFactor: 0.45);

        var result = sut.Score(data, NeutralWeights, Scoring);

        var horse = Assert.Single(result);
        Assert.Contains(horse.Reasons, static r => r.StartsWith("枠番評価 +0.10", StringComparison.Ordinal));
        Assert.Contains(horse.Reasons, static r => r.StartsWith("前走系評価 +1.00", StringComparison.Ordinal));
    }

    [Fact]
    public void Score_NarrowsGapCausedByPopularityCompared_ToBaseScorer()
    {
        var popularPastRuns = new[] { new RaceCardPastRun(1, null, null, "地方の平場戦", 1, 1) };
        var popularHorse = BuildEntry(1, 3, "人気馬", popularPastRuns);
        var plainHorse = BuildEntry(2, 3, "無印馬");
        var data = BuildData(popularHorse, plainHorse);

        var baseScores = new RuleBasedHorseScorer().Score(data, NeutralWeights, Scoring)
            .ToDictionary(static x => x.Name, static x => x.Score);
        var suppressedScores = new PopularitySuppressedHorseScorer(popularitySuppressionFactor: 0.45)
            .Score(data, NeutralWeights, Scoring)
            .ToDictionary(static x => x.Name, static x => x.Score);

        var baseGap = baseScores["人気馬"] - baseScores["無印馬"];
        var suppressedGap = suppressedScores["人気馬"] - suppressedScores["無印馬"];

        Assert.True(suppressedGap < baseGap, "人気評価の寄与を抑えた分、素点との差が縮まること");
    }
}
