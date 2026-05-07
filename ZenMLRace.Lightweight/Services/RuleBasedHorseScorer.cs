using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public sealed class RuleBasedHorseScorer : IHorseScorer
{
    private const double ScoreMin = -1.0;
    private const double ScoreMax = 1.0;

    public IReadOnlyList<HorseScore> Score(NormalizedRaceData normalizedData, WeightProfile weights, ScoringProfile scoringProfile)
    {
        var scores = new List<HorseScore>(normalizedData.Horses.Count);
        var resolvedWeights = new ResolvedWeights(weights);

        foreach (var horse in normalizedData.Horses)
        {
            var score = 50.0;
            var reasons = new List<string>();

            score += AddScore(
                reasons,
                "枠番評価",
                NormalizeScore(ScoreByBands(horse.FrameNumber, scoringProfile.FrameScores)),
                resolvedWeights.FrameWeight,
                weights.FrameWeight);

            if (horse.Age.HasValue)
            {
                score += AddScore(
                    reasons,
                    "年齢評価",
                    NormalizeScore(ScoreByBands(horse.Age.Value, scoringProfile.AgeScores)),
                    resolvedWeights.AgeWeight,
                    weights.AgeWeight);
            }

            var previousRaceRaw = 0.0;
            var hasPreviousRaceSignal = false;

            if (!string.IsNullOrWhiteSpace(horse.LastRaceCategory))
            {
                previousRaceRaw += NormalizeScore(ScoreLastRaceCategory(horse.LastRaceCategory, scoringProfile.LastRaceCategoryScores));
                hasPreviousRaceSignal = true;
            }

            if (horse.LastRaceFinishPosition.HasValue)
            {
                previousRaceRaw += NormalizeScore(ScoreByBands(horse.LastRaceFinishPosition.Value, scoringProfile.LastFinishScores));
                hasPreviousRaceSignal = true;
            }

            if (hasPreviousRaceSignal)
            {
                score += AddScore(
                    reasons,
                    "前走総合評価",
                    previousRaceRaw,
                    resolvedWeights.PreviousRaceWeight,
                    weights.PreviousRaceWeight);
            }

            if (horse.LastRacePopularity.HasValue)
            {
                score += AddScore(
                    reasons,
                    "人気評価",
                    NormalizeScore(ScoreByBands(horse.LastRacePopularity.Value, scoringProfile.LastPopularityScores)),
                    resolvedWeights.PopularityWeight,
                    weights.PopularityWeight);
            }

            scores.Add(new HorseScore(horse.Name, horse.HorseNumber, Math.Round(score, 2), reasons));
        }

        return scores.OrderByDescending(static x => x.Score).ToArray();
    }

    private static double ScoreLastRaceCategory(string? category, IReadOnlyDictionary<string, double> scores)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return 0.0;
        }

        return scores.TryGetValue(category, out var score) ? score : 0.0;
    }

    private static double AddScore(ICollection<string> reasons, string label, double rawScore, double resolvedWeight, double? rawWeight)
    {
        var weighted = rawScore * resolvedWeight;
        reasons.Add($"{label} {weighted:+0.00;-0.00} (w={FormatWeight(rawWeight)})");
        return weighted;
    }

    private static double NormalizeScore(double rawScore) => Math.Clamp(rawScore, ScoreMin, ScoreMax);

    private static double ScoreByBands(int value, IReadOnlyList<NumericBandScore> bands)
    {
        foreach (var band in bands)
        {
            var minOk = !band.MinInclusive.HasValue || value >= band.MinInclusive.Value;
            var maxOk = !band.MaxInclusive.HasValue || value <= band.MaxInclusive.Value;

            if (minOk && maxOk)
            {
                return band.Score;
            }
        }

        return 0.0;
    }

    private static double ResolveWeight(double? weight)
    {
        // 重みがなければ適用しない（中立=1.0）。
        return weight ?? 1.0;
    }

    private static string FormatWeight(double? weight)
    {
        return weight.HasValue ? weight.Value.ToString("0.##") : "none(1.0)";
    }

    private sealed record ResolvedWeights(WeightProfile Raw)
    {
        public double PopularityWeight { get; } = ResolveWeight(Raw.PopularityWeight);
        public double AgeWeight { get; } = ResolveWeight(Raw.AgeWeight);
        public double FrameWeight { get; } = ResolveWeight(Raw.FrameWeight);
        public double PreviousRaceWeight { get; } = ResolveWeight(Raw.PreviousRaceWeight);
    }
}
