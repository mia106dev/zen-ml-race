using ZenMLRace.Lightweight.Contracts;
using ZenMLRace.Lightweight.Services;

namespace ZenMLRace.Lightweight.Tests;

public class NormarizerTest
{
    [Fact]
    public void Normalize_RaceCardHtml_ParsesRealSanitizedFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Resources", "racecard.html");
        var raceCardHtml = File.ReadAllText(fixturePath);
        var source = new RaceSourceDocuments(
            "takamatsu-2026",
            raceCardHtml,
            DataHtml);
        var sut = new JraHtmlRaceNormalizer();

        var result = sut.Normalize(source);

        Assert.Equal("高松宮記念", result.RaceName);
        Assert.NotNull(result.RaceCard);
        Assert.Equal(result.Horses.Count, result.RaceCard!.Entries.Count);
        Assert.True(result.Horses.Count >= 16, "出馬表の頭数を十分に抽出できること");
        Assert.Contains(result.Horses, static x => x.Name == "サトノレーヴ");
    }

    private const string DataHtml = """
        <!doctype html>
        <html lang="ja">
        <body>
          <div id="data_analysis">
            <div class="block_unit">
              <div class="head_unit">
                <h3 class="head pop">人気</h3>
              </div>
              <div class="txt">
                <p>2番人気が8連対</p>
              </div>
            </div>
          </div>
        </body>
        </html>
        """;
}
