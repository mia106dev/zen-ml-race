using ZenMLRace.Lightweight.Contracts;
using ZenMLRace.Lightweight.Services;

namespace ZenMLRace.Lightweight.Tests;

public class NormarizerTest
{
    [Fact]
    public void Normalize_RaceCardHtml_ExtractsHorseProfilesWithLatestPastRace()
    {
        var source = new RaceSourceDocuments(
            "takamatsu",
            RaceCardHtml,
            DataHtml);
        var sut = new JraHtmlRaceNormalizer();

        var result = sut.Normalize(source);

        Assert.Equal("高松宮記念", result.RaceName);
        Assert.Equal(2, result.Horses.Count);

        var first = result.Horses[0];
        Assert.Equal(1, first.FrameNumber);
        Assert.Equal(1, first.HorseNumber);
        Assert.Equal("マッドクール", first.Name);
        Assert.Equal("Ocean", first.LastRaceCategory);
        Assert.Equal(2, first.LastRaceFinishPosition);
        Assert.Equal(5, first.LastRacePopularity);

        var second = result.Horses[1];
        Assert.Equal(2, second.FrameNumber);
        Assert.Equal(2, second.HorseNumber);
        Assert.Equal("サトノレーヴ", second.Name);
        Assert.Equal("Overseas", second.LastRaceCategory);
        Assert.Equal(3, second.LastRaceFinishPosition);
        Assert.Null(second.LastRacePopularity);
    }

    private const string RaceCardHtml = """
        <!doctype html>
        <html lang="ja">
        <body>
          <div id="race_title"><h1>高松宮記念</h1></div>
          <div id="main_contents">
            <div id="syutsuba">
              <div class="race_header">
                <div class="race_title">
                  <span class="race_name">高松宮記念</span>
                </div>
              </div>
              <table class="basic">
                <tbody>
                  <tr>
                    <td class="waku"><img alt="枠1白"></td>
                    <td class="num">1</td>
                    <td class="horse">
                      <div class="name_line"><div class="name">マッドクール</div></div>
                    </td>
                    <td class="jockey">
                      <p class="age">牡6/芦</p>
                    </td>
                    <td class="past p1">
                      <div class="race_line"><div class="name">オーシャンS</div></div>
                      <div class="place_line">
                        <div class="place">2着</div>
                        <div class="num"><span class="pop">5番人気</span></div>
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td class="waku"><img alt="枠2黒"></td>
                    <td class="num">2</td>
                    <td class="horse">
                      <div class="name_line"><div class="name">サトノレーヴ</div></div>
                    </td>
                    <td class="jockey">
                      <p class="age">牡6/鹿</p>
                    </td>
                    <td class="past p1">
                      <div class="race_line"><div class="name">香港スプリント</div></div>
                      <div class="place_line">
                        <div class="place">3着</div>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </body>
        </html>
        """;

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
