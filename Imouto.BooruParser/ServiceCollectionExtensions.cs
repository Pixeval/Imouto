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
    public static IServiceCollection AddBooruParsers(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IFlurlClientCache>(_ => new FlurlClientCache());

        services.AddSingleton<ISankakuAuthManager, SankakuAuthManager>();
        services.AddKeyedSingleton<ISankakuAuthManager>(
            IPlatformInfo.Sankaku,
            (provider, _) => provider.GetRequiredService<ISankakuAuthManager>());

        services.AddSingleton<DanbooruApiLoader>();
        services.AddSingleton<YandereApiLoader>();
        services.AddSingleton<SankakuApiLoader>();
        services.AddSingleton<GelbooruApiLoader>();
        services.AddSingleton<Rule34ApiLoader>();

        services.AddSingleton<IBooruApiLoader>(x => x.GetRequiredService<DanbooruApiLoader>());
        services.AddSingleton<IBooruApiLoader>(x => x.GetRequiredService<YandereApiLoader>());
        services.AddSingleton<IBooruApiLoader>(x => x.GetRequiredService<SankakuApiLoader>());
        services.AddSingleton<IBooruApiLoader>(x => x.GetRequiredService<GelbooruApiLoader>());
        services.AddSingleton<IBooruApiLoader>(x => x.GetRequiredService<Rule34ApiLoader>());

        services.AddSingleton<IBooruApiAccessor>(x => x.GetRequiredService<DanbooruApiLoader>());
        services.AddSingleton<IBooruApiAccessor>(x => x.GetRequiredService<YandereApiLoader>());
        services.AddSingleton<IBooruApiAccessor>(x => x.GetRequiredService<SankakuApiLoader>());

        services.AddKeyedSingleton<IGetArtworkService>(
            IPlatformInfo.Danbooru,
            (provider, _) => provider.GetRequiredService<DanbooruApiLoader>());
        services.AddKeyedSingleton<IGetArtworkService>(
            IPlatformInfo.Yandere,
            (provider, _) => provider.GetRequiredService<YandereApiLoader>());
        services.AddKeyedSingleton<IGetArtworkService>(
            IPlatformInfo.Sankaku,
            (provider, _) => provider.GetRequiredService<SankakuApiLoader>());
        services.AddKeyedSingleton<IGetArtworkService>(
            IPlatformInfo.Gelbooru,
            (provider, _) => provider.GetRequiredService<GelbooruApiLoader>());
        services.AddKeyedSingleton<IGetArtworkService>(
            IPlatformInfo.Rule34,
            (provider, _) => provider.GetRequiredService<Rule34ApiLoader>());

        services.AddKeyedSingleton<IDownloadHttpClientService, DanbooruImageDownloader>(IPlatformInfo.Danbooru);
        services.AddKeyedSingleton<IDownloadHttpClientService, GeneralImageDownloader>(IPlatformInfo.All);

        services.Configure<DanbooruSettings>(_ => { });
        services.Configure<YandereSettings>(_ => { });
        services.Configure<SankakuSettings>(_ => { });
        services.Configure<GelbooruSettings>(_ => { });
        services.Configure<Rule34Settings>(_ => { });

        return services;
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

public class DanbooruImageDownloader : IDownloadHttpClientService
{
    public string Platform => IPlatformInfo.All;

    private static readonly Lazy<HttpClient> _HttpClient = new(() =>
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new("gdl", "1.24.5"));
        return client;
    });

    public HttpClient GetApiClient() => _HttpClient.Value;

    public HttpClient GetImageDownloadClient() => _HttpClient.Value;
}
