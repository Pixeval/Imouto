using System.Net.Http.Headers;
using Flurl.Http.Configuration;
using Imouto.BooruParser.Implementations.Danbooru;
using Imouto.BooruParser.Implementations.Gelbooru;
using Imouto.BooruParser.Implementations.Rule34;
using Imouto.BooruParser.Implementations.Sankaku;
using Imouto.BooruParser.Implementations.Yandere;
using Microsoft.Extensions.DependencyInjection;
using Misaki;

namespace Imouto.BooruParser;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Should:
    ///     Add memory cache
    ///     Configure SankakuSettings, YandereSettings, DanbooruSettings
    /// </summary>
    public static IServiceCollection AddBooruParsers(this IServiceCollection services)
    {
        return services.AddSingleton<IFlurlClientCache>(_ => new FlurlClientCache())
            .AddKeyedSingleton<IDownloadHttpClientService, GeneralImageDownloader>(IPlatformInfo.All)
            .AddKeyedSingleton<IGetArtworkService, DanbooruApiLoader>(IPlatformInfo.Danbooru)
            .Configure<DanbooruSettings>(_ => { })
            .AddKeyedSingleton<IGetArtworkService, YandereApiLoader>(IPlatformInfo.Yandere)
            .Configure<YandereSettings>(_ => { })
            .AddKeyedSingleton<IGetArtworkService, SankakuApiLoader>(IPlatformInfo.Sankaku)
            .Configure<SankakuSettings>(_ => { })
            .AddKeyedSingleton<IGetArtworkService, GelbooruApiLoader>(IPlatformInfo.Gelbooru)
            .Configure<GelbooruSettings>(_ => { })
            .AddKeyedSingleton<IGetArtworkService, Rule34ApiLoader>(IPlatformInfo.Rule34)
            .Configure<Rule34Settings>(_ => { })
            .AddKeyedSingleton<ISankakuAuthManager, SankakuAuthManager>(IPlatformInfo.Sankaku);
    }
}

public class GeneralImageDownloader : IDownloadHttpClientService
{
    public string Platform => IPlatformInfo.All;

    private static readonly Lazy<HttpClient> _HttpClient = new(() =>
    {
        var client = new HttpClient();
        IReadOnlyList<ProductInfoHeaderValue> ua =
        [
            new("Mozilla", "5.0"),
            new("(Windows NT 10.0; Win64; x64)"),
            new("AppleWebKit", "537.36"),
            new("(KHTML, like Gecko)"),
            new("Chrome", "133.0.0.0"),
            new("Safari", "537.36"),
            new("Edg", "133.0.0.0")
        ];
        foreach (var item in ua) 
            client.DefaultRequestHeaders.UserAgent.Add(item);
        return client;
    });

    public HttpClient GetApiClient() => _HttpClient.Value;

    public HttpClient GetImageDownloadClient() => _HttpClient.Value;
}
