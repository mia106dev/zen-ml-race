using System.Text;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public sealed class DeterministicInsightNarrator : IInsightNarrator
{
    public string BuildNarrative(PredictionResult result)
    {
        var top = result.Ranking.Take(3).ToArray();
        if (top.Length == 0)
        {
            return "有効な馬データを抽出できなかったため、見解を生成できませんでした。";
        }

        var sb = new StringBuilder();
        sb.AppendLine("パドック前参考見解（出馬表+JRAデータ分析のみ）");
        sb.AppendLine($"1位候補: {top[0].Name}（{top[0].Score:F2}）");

        if (top.Length > 1)
        {
            sb.AppendLine($"2位候補: {top[1].Name}（{top[1].Score:F2}）");
        }

        if (top.Length > 2)
        {
            sb.AppendLine($"3位候補: {top[2].Name}（{top[2].Score:F2}）");
        }

        sb.AppendLine("上位評価は、年齢レンジ・前走カテゴリ・直近着順の合成スコアを根拠としています。");
        return sb.ToString().TrimEnd();
    }
}
