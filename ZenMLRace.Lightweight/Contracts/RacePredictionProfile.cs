namespace ZenMLRace.Lightweight.Contracts;

public sealed record RacePredictionProfile(
    string ProfileVersion,
    int TargetYear,
    string RaceKey,
    WeightProfile Weights,
    ScoringProfile Scoring);

public sealed record WeightProfile(
    double? PopularityWeight,
    double? AgeWeight,
    double? FrameWeight,
    double? PreviousRaceWeight);

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
