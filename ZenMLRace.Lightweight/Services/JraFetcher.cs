using System.Text;
using ZenMLRace.Lightweight.Contracts;

namespace ZenMLRace.Lightweight.Services;

public class JraFetcher
{
    private static readonly string baseUrl = "https://www.jra.go.jp/keiba/g1/";
    private readonly string raceKey;
    private readonly string raceCardUrl;
    private readonly string dataUrl;

    private readonly HttpClient httpClient;
    private static readonly string userAgentHeader = "Zen-Lightweight/0.1 (+https://www.jra.go.jp/)";

    private readonly Encoding sjis;
    public JraFetcher(string raceKey)
    {
        this.raceKey = raceKey;
        raceCardUrl = $"{baseUrl}{raceKey}/syutsuba.html";
        dataUrl = $"{baseUrl}{raceKey}/data.html";

        // Init HttpClient
        httpClient = new();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgentHeader);

        // Init Encoding(Use SJIS)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        sjis = Encoding.GetEncoding("shift_jis");
    }

    public async Task<RaceSourceDocuments> Fetch()
    {
        var raceCardHtml = await FetchHtmlAsync(raceCardUrl);
        var dataPage = await FetchHtmlAsync(dataUrl);

        return new(raceKey, raceCardHtml, dataPage);
    }

    private async Task<string> FetchHtmlAsync(string url)
    {
        using var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return sjis.GetString(bytes);
    }

}
