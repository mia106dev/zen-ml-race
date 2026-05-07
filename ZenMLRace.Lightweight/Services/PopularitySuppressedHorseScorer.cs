using System.Globalization;
using System.Text.RegularExpressions;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

/// <summary>
/// 既存スコアラーの結果をベースに「人気評価」寄与のみを抑える派生ストラテジ。
/// </summary>
public sealed class PopularitySuppressedHorseScorer : IHorseScorer
{
    private static readonly Regex reasonValueRegex = new(@"([+\-]\d+\.\d+)", RegexOptions.Compiled);
    private readonly IHorseScorer inner;
    private readonly double popularitySuppressionFactor;

    public PopularitySuppressedHorseScorer(IHorseScorer? inner = null, double popularitySuppressionFactor = 0.45)
    {
        this.inner = inner ?? new RuleBasedHorseScorer();
        this.popularitySuppressionFactor = Math.Clamp(popularitySuppressionFactor, 0.0, 1.0);
    }

    public IReadOnlyList<HorseScore> Score(
        NormalizedRaceData normalizedData,
        WeightProfile weights,
        ScoringProfile scoringProfile)
    {
        var baseScores = inner.Score(normalizedData, weights, scoringProfile);
        var adjusted = new List<HorseScore>(baseScores.Count);

        foreach (var horse in baseScores)
        {
            var score = horse.Score;
            var reasons = new List<string>(horse.Reasons.Count);

            foreach (var reason in horse.Reasons)
            {
                if (!reason.StartsWith("人気評価 ", StringComparison.Ordinal))
                {
                    reasons.Add(reason);
                    continue;
                }

                var match = reasonValueRegex.Match(reason);
                if (!match.Success
                    || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var originalValue))
                {
                    reasons.Add(reason);
                    continue;
                }

                var suppressedValue = originalValue * popularitySuppressionFactor;
                score += suppressedValue - originalValue;

                var replacedReason = BuildSuppressedPopularityReason(reason, suppressedValue);

                reasons.Add($"{replacedReason} [suppressed x{popularitySuppressionFactor:0.##}]");
            }

            adjusted.Add(new HorseScore(horse.Name, horse.HorseNumber, Math.Round(score, 2), reasons));
        }

        return adjusted
            .OrderByDescending(static x => x.Score)
            .ToArray();
    }

    private static string BuildSuppressedPopularityReason(string originalReason, double suppressedValue)
    {
        var formatted = suppressedValue.ToString("+0.00;-0.00", CultureInfo.InvariantCulture);
        var suffixIndex = originalReason.IndexOf(" (w=", StringComparison.Ordinal);
        if (suffixIndex < 0)
        {
            return $"人気評価 {formatted}";
        }

        var suffix = originalReason[suffixIndex..];
        return $"人気評価 {formatted}{suffix}";
    }
}
