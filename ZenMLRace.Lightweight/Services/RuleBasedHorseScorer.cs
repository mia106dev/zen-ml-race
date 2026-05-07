using ZenMLRace.Lightweight.Contracts;
using System.Text;
using System.Text.RegularExpressions;

namespace ZenMLRace.Lightweight.Services;

public sealed class RuleBasedHorseScorer : IHorseScorer
{
    private const double ScoreMin = -1.0;
    private const double ScoreMax = 1.0;
    private const double PreferredRaceMatchedBonus = 0.12;
    private const double PreferredRaceUnmatchedPenalty = -0.06;
    private const double FinishSignalPriorityFactor = 1.15;
    private const double PopularityImpactCap = 0.90;
    private const double PopularityImpactWithFinishFactor = 0.70;
    private const double PopularityImpactWithoutFinishFactor = 0.90;
    private static readonly Regex collapseWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<int, double> pastRunDecayByIndex = new Dictionary<int, double>
    {
        [1] = 1.00,
        [2] = 0.70,
        [3] = 0.50,
        [4] = 0.35
    };

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

            var pastRuns = ResolvePastRuns(normalizedData, horse);
            var previousRaceRaw = 0.0;
            var popularityRaw = 0.0;
            var previousRaceDecayTotal = 0.0;
            var popularityDecayTotal = 0.0;
            var hasPreviousRaceSignal = false;
            var hasPopularitySignal = false;

            foreach (var run in pastRuns)
            {
                var decay = ResolvePastRunDecay(run.Index);
                var runRaw = 0.0;
                var hasRunSignal = false;

                if (string.IsNullOrWhiteSpace(run.RaceName))
                {
                    throw new InvalidOperationException(
                        $"過去走(Index={run.Index})のRaceNameが空です。horse={horse.Name} horseNumber={horse.HorseNumber}");
                }

                var preferredRaceSignal = ScorePreferredRaceName(run.RaceName, scoringProfile.PreferredRaceNameScores);
                runRaw += preferredRaceSignal.Score;
                hasRunSignal = true;

                if (run.FinishPosition.HasValue)
                {
                    var finishSignal = NormalizeScore(ScoreByBands(run.FinishPosition.Value, scoringProfile.LastFinishScores));
                    runRaw += finishSignal * FinishSignalPriorityFactor;
                    hasRunSignal = true;
                }

                if (hasRunSignal)
                {
                    previousRaceRaw += runRaw * decay;
                    previousRaceDecayTotal += decay;
                    hasPreviousRaceSignal = true;
                }

                if (run.Popularity.HasValue)
                {
                    var popularityScore = NormalizeScore(ScoreByBands(run.Popularity.Value, scoringProfile.LastPopularityScores));
                    var popularityImpact = ResolvePopularityImpact(popularityScore, run.FinishPosition.HasValue);
                    popularityRaw += popularityImpact * decay;
                    popularityDecayTotal += decay;
                    hasPopularitySignal = true;
                }
            }

            if (hasPreviousRaceSignal && previousRaceDecayTotal > 0)
            {
                previousRaceRaw /= previousRaceDecayTotal;
            }

            if (hasPopularitySignal && popularityDecayTotal > 0)
            {
                popularityRaw /= popularityDecayTotal;
                popularityRaw = Math.Clamp(popularityRaw, -PopularityImpactCap, PopularityImpactCap);
            }

            if (hasPreviousRaceSignal)
            {
                score += AddScore(
                    reasons,
                    "前走系評価",
                    previousRaceRaw,
                    resolvedWeights.PreviousRaceWeight,
                    weights.PreviousRaceWeight);
            }

            if (hasPopularitySignal)
            {
                score += AddScore(
                    reasons,
                    "人気評価",
                    popularityRaw,
                    resolvedWeights.PopularityWeight,
                    weights.PopularityWeight);
            }

            scores.Add(new HorseScore(horse.Name, horse.HorseNumber, Math.Round(score, 2), reasons));
        }

        return scores.OrderByDescending(static x => x.Score).ToArray();
    }

    private static IReadOnlyList<PastRunForScoring> ResolvePastRuns(NormalizedRaceData normalizedData, HorseProfile horse)
    {
        var raceCardEntry = normalizedData.RaceCard?.Entries.FirstOrDefault(
            entry => entry.HorseNumber == horse.HorseNumber || string.Equals(entry.HorseName, horse.Name, StringComparison.Ordinal));

        if (raceCardEntry is not null && raceCardEntry.PastRuns.Count > 0)
        {
            return raceCardEntry.PastRuns
                .OrderBy(static x => x.Index)
                .Select(static x => new PastRunForScoring(x.Index, x.RaceName, x.FinishPosition, x.Popularity))
                .ToArray();
        }

        if (horse.LastRaceName is null && horse.LastRaceFinishPosition <= 0 && horse.LastRacePopularity <= 0)
        {
            return [];
        }

        return
        [
            new PastRunForScoring(
                1,
                horse.LastRaceName,
                horse.LastRaceFinishPosition > 0 ? horse.LastRaceFinishPosition : null,
                horse.LastRacePopularity > 0 ? horse.LastRacePopularity : null)
        ];
    }

    private static PreferredRaceSignal ScorePreferredRaceName(string raceName, IReadOnlyDictionary<string, double> scores)
    {
        if (scores.Count == 0)
        {
            return new PreferredRaceSignal(0.0, false);
        }

        if (scores.TryGetValue(raceName, out var exactScore))
        {
            return new PreferredRaceSignal(
                NormalizeScore(exactScore + PreferredRaceMatchedBonus),
                true);
        }

        var normalizedRaceName = NormalizeRaceNameForMatch(raceName);
        var bestScore = 0.0;
        var matched = false;
        foreach (var pair in scores)
        {
            var normalizedKey = NormalizeRaceNameForMatch(pair.Key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            // 香港スプリント vs 香港スプリン のような省略/表記ゆれを吸収する。
            if (normalizedRaceName.Contains(normalizedKey, StringComparison.Ordinal)
                || normalizedKey.Contains(normalizedRaceName, StringComparison.Ordinal))
            {
                bestScore = Math.Max(bestScore, pair.Value);
                matched = true;
            }
        }

        if (matched)
        {
            return new PreferredRaceSignal(
                NormalizeScore(bestScore + PreferredRaceMatchedBonus),
                true);
        }

        return new PreferredRaceSignal(PreferredRaceUnmatchedPenalty, false);
    }

    private static string NormalizeRaceNameForMatch(string raceName)
    {
        var normalized = raceName.Normalize(NormalizationForm.FormKC).Trim();
        return collapseWhitespaceRegex.Replace(normalized, string.Empty);
    }

    private static double ResolvePastRunDecay(int index)
    {
        return pastRunDecayByIndex.TryGetValue(index, out var decay) ? decay : 0.25;
    }

    private static double ResolvePopularityImpact(double popularityScore, bool hasFinishSignal)
    {
        var factor = hasFinishSignal ? PopularityImpactWithFinishFactor : PopularityImpactWithoutFinishFactor;
        return popularityScore * factor;
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

    private sealed record PastRunForScoring(
        int Index,
        string? RaceName,
        int? FinishPosition,
        int? Popularity);

    private sealed record PreferredRaceSignal(double Score, bool IsMatched);
}
