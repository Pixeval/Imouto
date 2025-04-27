using System.Globalization;
using Flurl.Http;
using Flurl.Http.Configuration;
using HtmlAgilityPack;
using Imouto.BooruParser.Extensions;
using Microsoft.Extensions.Options;
using Misaki;

namespace Imouto.BooruParser.Implementations.Gelbooru;

public class GelbooruApiLoader(IFlurlClientCache factory, IOptions<GelbooruSettings> options)
    : IBooruApiLoader
{
    public string Platform => IPlatformInfo.Gelbooru;
    private const string BaseUrl = "https://gelbooru.com/";
    private readonly IFlurlClient _flurlClient = factory
        .GetForDomain(new(BaseUrl))
        .BeforeCall(_ => DelayWithThrottler(options));

    public async Task<Post> GetPostAsync(string postId)
    {
        // https://gelbooru.com/index.php?page=post&s=view&id=
        var postHtml = await _flurlClient.Request("index.php")
            .SetQueryParams(new
            {
                page = "post",
                s = "view",
                id = postId
            })
            .GetHtmlDocumentAsync();
        
        // https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&limit=1&id=
        var postJson = await _flurlClient.Request("index.php")
            .SetQueryParams(new
            {
                page = "dapi", s = "post", q = "index", json = 1, limit = 1, id = postId
            })
            .GetJsonAsync<GelbooruPostPage>();

        var post = postJson.Posts?.FirstOrDefault();

        return post is null
            ? CreatePost(postHtml)!
            : CreatePost(post, postHtml);
    }

    public async Task<Post?> GetPostByMd5Async(string md5)
    {
        // https://gelbooru.com/index.php?page=post&s=list&md5=
        var postHtml = await _flurlClient.Request("index.php")
            .SetQueryParams(new
            {
                page = "post",
                s = "list",
                md5
            })
            .WithAutoRedirect(true)
            .GetHtmlDocumentAsync();
        
        // https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&limit=1&md5=
        var postJson = await _flurlClient.Request("index.php")
            .SetQueryParams(new
            {
                page = "dapi", s = "post", q = "index", json = 1, limit = 1, tags = $"md5:{md5}"
            })
            .GetJsonAsync<GelbooruPostPage>();
        
        var post = postJson.Posts?.FirstOrDefault();
        
        return post is null
            ? CreatePost(postHtml)
            : CreatePost(post, postHtml);
    }

    public async Task<SearchResult> SearchAsync(string tags)
    {
        // https://gelbooru.com/index.php?page=dapi&s=post&q=index&json=1&limit=20&tags=1girl
        var postJson = await _flurlClient.Request("index.php")
            .SetQueryParam("page", "dapi")
            .SetQueryParam("s", "post")
            .SetQueryParam("q", "index")
            .SetQueryParam("json", 1)
            .SetQueryParam("limit", 20)
            .SetQueryParam("tags", tags)
            .SetQueryParam("pid", 0)
            .GetJsonAsync<GelbooruPostPage>();

        return new(postJson.Posts?
            .Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.Tags, false, false))
            .ToArray() ?? [], tags, 0);
    }

    public async Task<SearchResult> GetNextPageAsync(SearchResult results)
    {
        var nextPage = results.PageNumber + 1;

        var postJson = await _flurlClient.Request("index.php")
            .SetQueryParam("page", "dapi")
            .SetQueryParam("s", "post")
            .SetQueryParam("q", "index")
            .SetQueryParam("json", 1)
            .SetQueryParam("limit", 20)
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("pid", nextPage)
            .GetJsonAsync<GelbooruPostPage>();

        return new(postJson.Posts?
            .Select(x => new PostPreview(
                x.Id.ToString(),
                x.Md5,
                x.Tags,
                false,
                false))
            .ToArray() ?? [], results.SearchTags, nextPage);
    }

    public async Task<SearchResult> GetPreviousPageAsync(SearchResult results)
    {
        if (results.PageNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(results.PageNumber), results.PageNumber, null);

        var nextPage = results.PageNumber - 1;

        var postJson = await _flurlClient.Request("index.php")
            .SetQueryParam("page", "dapi")
            .SetQueryParam("s", "post")
            .SetQueryParam("q", "index")
            .SetQueryParam("json", 1)
            .SetQueryParam("limit", 20)
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("pid", nextPage)
            .GetJsonAsync<GelbooruPostPage>();

        return new(postJson.Posts?
            .Select(x => new PostPreview(
                x.Id.ToString(),
                x.Md5,
                x.Tags,
                false,
                false))
            .ToArray() ?? [], results.SearchTags, nextPage);
    }

    public Task<SearchResult> GetPopularPostsAsync(PopularType type)
        => throw new NotSupportedException("Gelbooru does not support popularity charts");

    public Task<HistorySearchResult<TagHistoryEntry>> GetTagHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
        => throw new NotSupportedException("Gelbooru does not support history");

    public Task<HistorySearchResult<NoteHistoryEntry>> GetNoteHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
        => throw new NotSupportedException("Gelbooru does not support history");

    private static IReadOnlyList<Note> GetNotes(GelbooruPost? post, HtmlDocument postHtml)
    {
        if (post?.HasNotes == "false")
            return [];

        var notes = postHtml.DocumentNode
            .SelectNodes("//*[@id='notes']/article")
            ?.Select(note =>
            {
                var height = note.Attributes["data-height"].Value;
                var width = note.Attributes["data-width"].Value;
                var top = note.Attributes["data-y"].Value;
                var left = note.Attributes["data-x"].Value;

                var size = new Size(GetSizeInt(width), GetSizeInt(height));
                var point = new Position(GetPositionInt(top), GetPositionInt(left));

                var id = Convert.ToInt32(note.Attributes["data-id"].Value);
                var text = note.InnerText;

                return new Note(id.ToString(), text, point, size);
            }) ?? [];

        return [.. notes];
        
        static int GetSizeInt(string number) => (int)(Convert.ToDouble(number) + 0.5);
        
        static int GetPositionInt(string number) => (int)Math.Ceiling(Convert.ToDouble(number) - 0.5);
    }

    private static IReadOnlyList<Tag> GetTags(HtmlDocument post) 
        => [.. post.DocumentNode
            .SelectSingleNode("//*[@id='tag-list']")
            .SelectNodes("li")
            .Where(x => x.Attributes["class"]?.Value.StartsWith("tag-type-") == true)
            .Select(x =>
            {
                var type = x.Attributes["class"].Value.Split('-')[^1];
                var name = x.SelectSingleNode("a").InnerHtml;

                return new Tag(type, name);
            })];

    /// <summary>
    /// Auth isn't supported right now.
    /// </summary>
    private static async Task DelayWithThrottler(IOptions<GelbooruSettings> options)
    {
        var delay = options.Value.PauseBetweenRequests;
        if (delay > TimeSpan.Zero)
            await Throttler.Get("gelbooru").UseAsync(delay);
    }

    private static DateTimeOffset ExtractDate(GelbooruPost post) =>
        // Sat Oct 22 02:03:36 -0500 2022
        DateTimeOffset.ParseExact(
            post.CreatedAt,
            "ddd MMM dd HH:mm:ss zzz yyyy",
            CultureInfo.InvariantCulture);

    private static Post CreatePost(GelbooruPost post, HtmlDocument postHtml)
        // Parent is always 0
        // No children
        => new(
            new(post.Id.ToString(), post.Md5, PlatformType.Gelbooru),
            post.FileUrl,
            string.IsNullOrWhiteSpace(post.SampleUrl) ? null : post.SampleUrl,
            post.PreviewUrl,
            ExistState.Exist,
            ExtractDate(post),
            new(post.CreatorId.ToString(), post.Owner.Replace('_', ' '), PlatformType.Gelbooru),
            post.Source,
            new(post.Width, post.Height),
            0,
            SafeRating.Parse(post.Rating),
            GetTags(postHtml),
            GetNotes(post, postHtml));

    private static Post? CreatePost(HtmlDocument postHtml)
    {
        var idString = postHtml.DocumentNode.SelectSingleNode("//head/link[@rel='canonical']")
            ?.Attributes["href"]?.Value?.Split('=')[^1];

        if (idString is null)
            return null;
        
        var id = int.Parse(idString);
        
        var url = postHtml.DocumentNode.SelectSingleNode("//head/meta[@property='og:image']").Attributes["content"].Value;
        var md5 = url.Split('/')[^1].Split('.')[0];
        
        var dateString = postHtml.DocumentNode.SelectSingleNode("//li[contains (., 'Posted: ')]/text()[1]").InnerText[8..];
        var date = new DateTimeOffset(DateTime.ParseExact(dateString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), TimeSpan.FromHours(-5));
        
        var uploader = postHtml.DocumentNode.SelectSingleNode("//li[contains (., 'Posted: ')]/a/text()")?.InnerText ?? "Anonymous";
        
        var source = postHtml.DocumentNode.SelectSingleNode("//li[contains (., 'Source: ')]/a[1]")?.Attributes["href"].Value;
        
        var sizeString = postHtml.DocumentNode.SelectSingleNode("//li[contains (., 'Size: ')]/text()").InnerText;
        var size = sizeString.Split(':')[^1].Trim().Split('x').Select(int.Parse).ToList();
        
        var rating = postHtml.DocumentNode.SelectSingleNode("//li[contains (., 'Rating: ')]/text()").InnerText.Split(' ')[^1].ToLower();

        return new(
            new(id.ToString(), md5, PlatformType.Gelbooru),
            url,
            null,
            null,
            ExistState.MarkDeleted,
            date,
            new("-1", uploader.Replace('_', ' '), PlatformType.Gelbooru),
            source,
            new(size[0], size[1]),
            0,
            SafeRating.Parse(rating),
            GetTags(postHtml),
            GetNotes(null, postHtml));
    }
}
