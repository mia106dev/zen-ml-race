namespace ZenMLRace.Lightweight.Contracts;

public sealed record NumericBandScore(
    int? MinInclusive,
    int? MaxInclusive,
    double Score);

public sealed record ScoringProfile(
    IReadOnlyList<NumericBandScore> FrameScores,
    IReadOnlyList<NumericBandScore> AgeScores,
    IReadOnlyDictionary<string, double> LastRaceCategoryScores,
    IReadOnlyList<NumericBandScore> LastFinishScores,
    IReadOnlyList<NumericBandScore> LastPopularityScores);
