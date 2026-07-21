using System.Globalization;
using System.Net;
using Flurl.Http;
using Flurl.Http.Configuration;
using HtmlAgilityPack;
using Imouto.BooruParser.Extensions;
using Imouto.BooruParser.Implementations.Danbooru;
using Microsoft.Extensions.Options;
using Misaki;

namespace Imouto.BooruParser.Implementations.Yandere;

// test 
/// correct time
/// parent md5 1032014
/// children md5s 1032017
/// pools 1032020
/// tags with type 729673
/// notes 729673
/// extensions for tags and notes
public class YandereApiLoader(IFlurlClientCache factory, IOptions<YandereSettings> options) : IBooruApiLoader, IBooruApiAccessor
{
    public string Platform => IPlatformInfo.Yandere;

    private const string BaseUrl = "https://yande.re";

    private readonly string _botUserAgent = options.Value.BotUserAgent ?? throw new("UserAgent is required to make api calls");
    private readonly IFlurlClient _flurlClient = factory.GetForDomain(new(BaseUrl)).BeforeCall(x => SetAuthParameters(x, options));

    public async Task<Post> GetPostAsync(string postId)
    {
        var posts = await _flurlClient
            .Request("post", "index.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", $"id:{postId}")
            .GetJsonAsync<YanderePost[]>();
        var post = posts.FirstOrDefault()
                   ?? throw new PostNotFoundException("Yande.re", postId);

        var postHtml = await _flurlClient
            .Request("post", "show", postId)
            .WithUserAgent(_botUserAgent)
            .GetHtmlDocumentAsync();

        return await GetPost(postId, post, postHtml);
    }

    public async Task<Post?> GetPostByMd5Async(string md5)
    {
        // https://yande.re/post.json?tags=md5%3Ae6500b62d4003a5f4ba226d3a665c25a
        var posts = await _flurlClient.Request("post.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", $"md5:{md5} holds:all")
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        if (posts.Count is 0)
            return null;
        var post = posts[0];

        var postHtml = await _flurlClient
            .Request("post", "show", post.Id)
            .WithUserAgent(_botUserAgent)
            .GetHtmlDocumentAsync();

        return await GetPost(post.Id.ToString(), post, postHtml);
    }

    /// <summary>
    /// Remember to include "holds:all" if you want to see all posts.
    /// </summary>
    public async Task<SearchResult> SearchAsync(string tags)
    {
        var posts = await _flurlClient.Request("post.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", tags)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.Tags, false, false))], tags, 1);
    }

    public async Task<SearchResult> GetNextPageAsync(SearchResult results)
    {
        var nextPage = results.PageNumber + 1;

        var posts = await _flurlClient.Request("post.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("page", nextPage)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([
            .. posts
                .Select(x => new PostPreview(
                    x.Id.ToString(),
                    x.Md5,
                    x.Tags,
                    false,
                    false))
        ], results.SearchTags, nextPage);
    }

    public async Task<SearchResult> GetPreviousPageAsync(SearchResult results)
    {
        if (results.PageNumber <= 1)
            throw new ArgumentOutOfRangeException(nameof(results.PageNumber), results.PageNumber, null);

        var nextPage = results.PageNumber - 1;

        var posts = await _flurlClient.Request("post.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("page", nextPage)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([
            .. posts
                .Select(x => new PostPreview(
                    x.Id.ToString(),
                    x.Md5,
                    x.Tags,
                    false,
                    false))
        ], results.SearchTags, nextPage);
    }

    public async Task<SearchResult> GetPopularPostsAsync(PopularType type)
    {
        var period = type switch
        {
            PopularType.Day => "1d",
            PopularType.Week => "1w",
            PopularType.Month => "1m",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        // https://yande.re/post/popular_recent.json?period=1w
        var posts = await _flurlClient.Request("post", "popular_recent.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("period", period)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.Tags, false, false))], "popular", 1);
    }

    public async Task<HistorySearchResult<TagHistoryEntry>> GetTagHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
    {
        var request = _flurlClient.Request("history")
            .WithUserAgent(_botUserAgent);

        if (token != null)
            request = request.SetQueryParam("page", token.Page);

        var pageHtml = await request.GetHtmlDocumentAsync(cancellationToken: ct);

        var rows = pageHtml.DocumentNode
            .SelectNodes("//*[@id='history']/tbody/tr")
            ?.Select(x =>
            {
                var id = int.Parse(x.Attributes["id"].Value[1..]);
                var data = x.SelectNodes("td")!;
                return (id, data);
            })
            .ToList() ?? [];

        var entries = rows
            .Where(x => x.data[0].InnerHtml is "Post")
            .Select(x =>
            {
                var data = x.data;
                var postId = int.Parse(data[2].ChildNodes[0].InnerHtml);
                var date = DateTimeOffset.Parse(
                    WebUtility.HtmlDecode(data[3].InnerText),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                int? parentId = null;
                var parentChanged = false;
                var parentNodes = data[5].SelectNodes("span/a");
                if (parentNodes?.Count is 1)
                {
                    parentId = int.Parse(parentNodes.First().InnerText);
                    parentChanged = true;
                }

                return new TagHistoryEntry(
                    x.id,
                    date,
                    postId.ToString(),
                    parentId?.ToString(),
                    parentChanged);
            });

        var nextPage = token?.Page switch
        {
            var x when int.TryParse(x, out var page) => (page + 1).ToString(),
            _ => "2"
        };

        var result = entries.ToList();
        return new HistorySearchResult<TagHistoryEntry>(
            result,
            rows.Count > 0 ? new SearchToken(nextPage) : null) { OldestHistoryId = rows.Count > 0 ? rows.Min(x => x.id) : null };
    }

    public async Task<HistorySearchResult<NoteHistoryEntry>> GetNoteHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
    {
        var request = _flurlClient.Request("note", "history")
            .WithUserAgent(_botUserAgent);

        if (token != null)
            request = request.SetQueryParam("page", token.Page);

        var pageHtml = await request.GetHtmlDocumentAsync(cancellationToken: ct);

        var entries = pageHtml.DocumentNode
            .SelectNodes("//*[@id='content']/table/tbody/tr")?
            .Select(x =>
            {
                var postId = int.Parse(x.SelectNodes("td")![1].SelectSingleNode("a")!.InnerHtml);
                var dateString = x.SelectNodes("td")![5].InnerHtml;
                var date = DateTimeOffset.ParseExact(
                    dateString,
                    "MM/dd/yy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);

                return new NoteHistoryEntry(-1, postId.ToString(), date);
            })
            .ToList() ?? [];

        var nextPage = token?.Page switch
        {
            var x when int.TryParse(x, out var page) => (page + 1).ToString(),
            _ => "2"
        };

        return new HistorySearchResult<NoteHistoryEntry>(
            entries,
            entries.Count > 0 ? new SearchToken(nextPage) : null);
    }

    public async Task<bool> PostFavoriteAsync(string postId, bool favorite, CancellationToken token = default)
    {
        if (!favorite)
            throw new NotSupportedException(favorite.ToString());
        await _flurlClient.Request("post", "vote.json")
            .WithUserAgent(_botUserAgent)
            .PostMultipartAsync(content => content
                .Add("id", new StringContent(postId))
                .Add("score", new StringContent("3")), cancellationToken: token);
        return true;
    }

    private async Task<PostIdentity> GetPostIdentityAsync(long postId)
    {
        var posts = await _flurlClient.Request("post", "index.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", $"id:{postId} holds:all")
            .GetJsonAsync<YanderePost[]>();

        var post = posts.First();

        return new(post.Id.ToString(), post.Md5, PlatformType.Yandere);
    }

    private async Task<IReadOnlyList<PostIdentity>> GetChildrenAsync(YanderePost post)
    {
        if (!post.HasChildren)
            return [];

        var children = await _flurlClient.Request("post.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("tags", $"parent:{post.Id} holds:all")
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return
        [
            .. children
                .Where(x => x.Id != post.Id)
                .OrderBy(x => x.Id)
                .Select(x => new PostIdentity(x.Id.ToString(), x.Md5, PlatformType.Yandere))
        ];
    }

    private static ExistState GetExistState(YanderePost post)
        => string.Equals(post.Status, "deleted", StringComparison.OrdinalIgnoreCase)
            ? ExistState.MarkDeleted
            : ExistState.Exist;

    private async Task<IReadOnlyList<Pool>> GetPoolsAsync(long postId, HtmlDocument postHtml)
    {
        var pools = postHtml.DocumentNode
            .SelectNodes("//*[@id='post-view']/div[@class='status-notice']")
            ?.Where(x => x.Attributes["id"]?.Value?[..4] is "pool")
            .Select(x => int.Parse(x.Attributes["id"].Value[4..])) ?? [];

        var tasks = pools.Select(poolId => GetPoolForPostAsync(poolId, postId)).ToList();
        await Task.WhenAll(tasks);
        return [.. tasks.Select(x => x.Result)];
    }

    private async Task<Pool> GetPoolForPostAsync(long poolId, long postId)
    {
        // https://yande.re/pool/show.json?id={id}
        var pool = await _flurlClient.Request("pool", "show.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("id", poolId)
            .GetJsonAsync<YanderePool>();

        return new(
            pool.Id.ToString(),
            pool.Name.Replace('_', ' '),
            Array.IndexOf([.. pool.Posts.Select(x => x.Id)], postId));
    }

    private async Task<IReadOnlyList<Note>> GetNotesAsync(YanderePost post)
    {
        if (post.LastNotedAt is 0)
            return [];

        var notes = await _flurlClient.Request("note.json")
            .WithUserAgent(_botUserAgent)
            .SetQueryParam("post_id", post.Id)
            .GetJsonAsync<IReadOnlyList<YandereNote>>();

        return
        [
            .. notes
                .Where(x => x.IsActive)
                .Select(x => new Note(
                    x.Id.ToString(),
                    WebUtility.HtmlDecode(x.Body),
                    new Position(x.Y, x.X),
                    new Size(x.Width, x.Height)))
        ];
    }

    private static IReadOnlyList<Tag> GetTags(HtmlDocument postHtml) =>
    [
        .. postHtml.DocumentNode
            .SelectSingleNode("//*[@id='tag-sidebar']")
            .SelectNodes("li")
            .Select(x =>
            {
                var type = x.Attributes["class"].Value.Split('-').Last();
                var aNode = x.SelectSingleNode(@"a[last()]")!;
                var name = aNode.InnerHtml;

                return new Tag(type, name);
            })
    ];

    private static async Task SetAuthParameters(FlurlCall call, IOptions<YandereSettings> options)
    {
        var login = options.Value.Login;
        var passwordHash = options.Value.PasswordHash;
        var delay = options.Value.PauseBetweenRequests;

        if (login != null && passwordHash != null)
            call.Request.SetQueryParam("login", login).SetQueryParam("password_hash", passwordHash);

        if (delay > TimeSpan.Zero)
            await Throttler.Get("yandere").UseAsync(delay);
    }

    private async Task<Post> GetPost(string postId, YanderePost post, HtmlDocument postHtml)
    {
        PostIdentity? parent = null;
        if (post.ParentId is { } parentId)
            parent = await GetPostIdentityAsync(parentId);

        var childrenTask = GetChildrenAsync(post);
        var poolsTask = GetPoolsAsync(post.Id, postHtml);
        var notesTask = GetNotesAsync(post);
        await Task.WhenAll(childrenTask, poolsTask, notesTask);

        var postIdentity = new PostIdentity(postId, post.Md5, PlatformType.Yandere);
        return new(
            postIdentity,
            post.FileUrl,
            post.SampleUrl,
            post.JpegUrl,
            GetExistState(post),
            DateTimeOffset.FromUnixTimeSeconds(post.CreatedAt),
            new(post.CreatorId?.ToString() ?? "-1", post.Author, PlatformType.Yandere),
            post.Source,
            new(post.Width, post.Height),
            post.FileSize,
            SafeRating.Parse(post.Rating),
            GetTags(postHtml),
            await notesTask,
            parent)
        {
            ChildrenIds = await childrenTask,
            Pools = await poolsTask
        };
    }
}
