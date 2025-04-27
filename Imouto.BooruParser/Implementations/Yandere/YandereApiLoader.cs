using System.Globalization;
using System.Text.RegularExpressions;
using Flurl.Http;
using Flurl.Http.Configuration;
using HtmlAgilityPack;
using Imouto.BooruParser.Extensions;
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

    private static readonly Regex NotePositionRegex = new(
            "width[:\\s]*(?<width>\\d+.{0,1}\\d*)px.*height[:\\s]*(?<height>\\d+.{0,1}\\d*)px.*top[:\\s]*(?<top>\\d+.{0,1}\\d*)px.*left[:\\s]*(?<left>\\d+.{0,1}\\d*)px",
            RegexOptions.Compiled);
    
    private const string BaseUrl = "https://yande.re";

    private readonly IFlurlClient _flurlClient = factory.GetForDomain(new(BaseUrl)).BeforeCall(x => SetAuthParameters(x, options));

    public async Task<Post> GetPostAsync(string postId)
    {
        var posts = await _flurlClient.Request("post", "index.json")
            .SetQueryParam("tags", $"id:{postId}")
            .GetJsonAsync<YanderePost[]>();
        var post = posts.First();

        var postHtml = await _flurlClient
            .Request("post", "show", postId)
            .GetHtmlDocumentAsync();

        return GetPost(postId, post, postHtml);
    }

    public async Task<Post?> GetPostByMd5Async(string md5)
    {
        // https://yande.re/post.json?tags=md5%3Ae6500b62d4003a5f4ba226d3a665c25a
        var posts = await _flurlClient.Request("post.json")
            .SetQueryParam("tags", $"md5:{md5} holds:all")
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        if (posts.Count is 0)
            return null;
        var post = posts[0];

        var postHtml = await _flurlClient
            .Request("post", "show", post.Id)
            .GetHtmlDocumentAsync();

        return GetPost(post.Id.ToString(), post, postHtml);
    }

    /// <summary>
    /// Remember to include "holds:all" if you want to see all posts.
    /// </summary>
    public async Task<SearchResult> SearchAsync(string tags)
    {
        var posts = await _flurlClient.Request("post.json")
            .SetQueryParam("tags", tags)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.Tags, false, false))], tags, 1);
    }

    public async Task<SearchResult> GetNextPageAsync(SearchResult results)
    {
        var nextPage = results.PageNumber + 1;

        var posts = await _flurlClient.Request("post.json")
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("page", nextPage)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([.. posts
            .Select(x => new PostPreview(
                x.Id.ToString(), 
                x.Md5, 
                x.Tags,
                false,
                false))], results.SearchTags, nextPage);
    }

    public async Task<SearchResult> GetPreviousPageAsync(SearchResult results)
    {
        if (results.PageNumber <= 1)
            throw new ArgumentOutOfRangeException(nameof(results.PageNumber), results.PageNumber, null);

        var nextPage = results.PageNumber - 1;

        var posts = await _flurlClient.Request("post.json")
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("page", nextPage)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([.. posts
            .Select(x => new PostPreview(
                x.Id.ToString(),
                x.Md5,
                x.Tags,
                false,
                false))], results.SearchTags, nextPage);
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
            .SetQueryParam("period", period)
            .GetJsonAsync<IReadOnlyList<YanderePost>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.Tags, false, false))], "popular", 1);
    }
    public async Task<HistorySearchResult<TagHistoryEntry>> GetTagHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
    {
        var request = _flurlClient.Request("history");

        if (token != null)
            request = request.SetQueryParam("page", token.Page);

        var pageHtml = await request.GetHtmlDocumentAsync(cancellationToken: ct);

        var entries = pageHtml.DocumentNode
            .SelectNodes("//*[@id='history']/tbody/tr")
            ?.Select(x =>
            {
                var id = int.Parse(x.Attributes["id"].Value[1..]);
                var data = x.SelectNodes("td");
                return (id, data);
            })
            .Where(x => x.data[0].InnerHtml is "Post")
            .Select(x =>
            {
                var data = x.data;
                var postId = int.Parse(data[2].ChildNodes[0].InnerHtml);
                var date = DateTime.Parse(data[3].InnerHtml);

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
                    new(date, TimeSpan.Zero),
                    postId.ToString(),
                    parentId?.ToString(),
                    parentChanged);
            }) ?? [];

        var nextPage = token?.Page switch
        {
            var x when int.TryParse(x, out var page) => (page + 1).ToString(),
            _ => "2"
        };

        return new([.. entries], new(nextPage));
    }

    public async Task<HistorySearchResult<NoteHistoryEntry>> GetNoteHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
    {
        var request = _flurlClient.Request("note", "history");

        if (token != null)
            request = request.SetQueryParam("page", token.Page);

        var pageHtml = await request.GetHtmlDocumentAsync(cancellationToken: ct);

        var entries = pageHtml.DocumentNode
            .SelectNodes("//*[@id='content']/table/tbody/tr")
            .Select(x =>
            {
                var postId = int.Parse(x.SelectNodes("td")[1].SelectSingleNode("a").InnerHtml);
                var dateString = x.SelectNodes("td")[5].InnerHtml;
                var date = DateTime.ParseExact(dateString, "MM/dd/yy", CultureInfo.InvariantCulture);

                return new NoteHistoryEntry(-1, postId.ToString(), date);
            })
            .ToList();

        var nextPage = token?.Page switch
        {
            var x when int.TryParse(x, out var page) => (page + 1).ToString(),
            _ => "2"
        };

        return new(entries, new(nextPage));
    }

    public async Task<bool> PostFavoriteAsync(string postId, bool favorite)
    {
        if (!favorite)
            throw new NotSupportedException(favorite.ToString());
        await _flurlClient.Request("post", "vote.json")
            .PostMultipartAsync(content => content
                .Add("id", new StringContent(postId))
                .Add("score", new StringContent("3")));
        return true;
    }

    private async Task<PostIdentity> GetPostIdentityAsync(int postId)
    {
        var posts = await _flurlClient.Request("post", "index.json")
            .SetQueryParam("tags", $"id:{postId} holds:all")
            .GetJsonAsync<YanderePost[]>();

        var post = posts.First();

        return new(post.Id.ToString(), post.Md5, PlatformType.Yandere);
    }

    private static ExistState GetExistState(HtmlDocument postHtml)
    {
        var isDeleted = postHtml.DocumentNode
            .SelectNodes("//*[@id='post-view']/div[@class='status-notice']")
            ?.Any(x => x.InnerHtml.Contains("This post was deleted.")) ?? false;

        return isDeleted ? ExistState.MarkDeleted : ExistState.Exist;
    }

    private async Task<Pool> GetPoolForPostAsync(long poolId, long postId)
    {
        // https://yande.re/pool/show.json?id={id}
        var pool = await _flurlClient.Request("pool", "show.json")
            .SetQueryParam("id", poolId)
            .GetJsonAsync<YanderePool>();

        return new(
            pool.Id.ToString(),
            pool.Name.Replace('_', ' '),
            Array.IndexOf([.. pool.Posts.Select(x => x.Id)], postId));
    }

    private static IReadOnlyList<Note> GetNotes(YanderePost post, HtmlDocument postHtml)
    {
        if (post.LastNotedAt is 0)
            return [];

        var notes = postHtml.DocumentNode
            .SelectSingleNode("//*[@id='note-container']")
            ?.SelectNodes("div")
            ?.SelectPairs((styleNode, textNode) =>
            {
                var stylesStrings = styleNode.Attributes["style"].Value;
                var match = NotePositionRegex.Match(stylesStrings);

                var height = match.Groups["height"].Value;
                var width = match.Groups["width"].Value;
                var top = match.Groups["top"].Value;
                var left = match.Groups["left"].Value;

                var size = new Size(GetSizeInt(width), GetSizeInt(height));
                var point = new Position(GetPositionInt(top), GetPositionInt(left));

                var id = Convert.ToInt32(textNode.Attributes["id"].Value.Split('-').Last());
                var text = textNode.InnerHtml;

                return new Note(id.ToString(), text, point, size);
            }) ?? [];

        return [.. notes];
        
        static int GetSizeInt(string number) => (int)(Convert.ToDouble(number) + 0.5);
        
        static int GetPositionInt(string number) => (int)Math.Ceiling(Convert.ToDouble(number) - 0.5);
    }

    private static IReadOnlyList<Tag> GetTags(HtmlDocument postHtml) =>
        [.. postHtml.DocumentNode
            .SelectSingleNode("//*[@id='tag-sidebar']")
            .SelectNodes("li")
            .Select(x =>
            {
                var type = x.Attributes["class"].Value.Split('-').Last();
                var aNode = x.SelectSingleNode("a[2]");
                var name = aNode.InnerHtml;

                return new Tag(type, name);
            })];

    private static async Task SetAuthParameters(FlurlCall call, IOptions<YandereSettings> options)
    {
        var login = options.Value.Login;
        var passwordHash = options.Value.PasswordHash;
        var delay = options.Value.PauseBetweenRequests;

        if (login != null && passwordHash != null)
            call.Request.SetQueryParam("login", login).SetQueryParam("password_hash", passwordHash);

        if (delay > TimeSpan.Zero)
            await Throttler.Get("Yandere").UseAsync(delay);
    }

    private Post GetPost(string postId, YanderePost post, HtmlDocument postHtml)
    {
        var postIdentity = new PostIdentity(postId, post.Md5, PlatformType.Yandere);
        return new(
            postIdentity,
            post.FileUrl,
            post.SampleUrl,
            post.JpegUrl,
            GetExistState(postHtml),
            DateTimeOffset.FromUnixTimeSeconds(post.CreatedAt),
            new(post.CreatorId?.ToString() ?? "-1", post.Author, PlatformType.Yandere),
            post.Source,
            new(post.Width, post.Height),
            post.FileSize,
            SafeRating.Parse(post.Rating),
            GetTags(postHtml),
            GetNotes(post, postHtml),
            postIdentity.TryFork(post.ParentId, ""))
        {
            ChildrenIdsGetter = GetChildrenAsync,
            PoolsGetter = GetPoolsAsync
        };

        async Task<IReadOnlyList<PostIdentity>> GetChildrenAsync(Post p)
        {
            var childrenIds = postHtml.DocumentNode
                .SelectNodes("//*[@id='post-view']/div[@class='status-notice']")
                ?.FirstOrDefault(x => x.InnerHtml.Contains("child post"))
                ?.SelectNodes("a").Where(x => x.Attributes["href"]?.Value.Contains("/post/show/") ?? false)
                .Select(x => int.Parse(x.InnerHtml))
                .ToArray() ?? [];

            if (childrenIds.Length is 0)
                return [];

            var childrenTasks = childrenIds.Select(GetPostIdentityAsync).ToList();

            await Task.WhenAll(childrenTasks);

            return [.. childrenTasks.Select(x => x.Result)];
        }

        async Task<IReadOnlyList<Pool>> GetPoolsAsync(Post p)
        {
            var pools = postHtml.DocumentNode
                .SelectNodes("//*[@id='post-view']/div[@class='status-notice']")
                ?.Where(x => x.Attributes["id"]?.Value?[..4] == "pool")
                .Select(x =>
                {
                    var id = int.Parse(x.Attributes["id"].Value[4..]);
                    var aNodes = x.SelectNodes("div/p/a");
                    var poolNode = aNodes.Last(y => y.Attributes["href"].Value[..5] == "/pool");
                    var name = poolNode.InnerHtml;

                    return (id, name);
                }) ?? [];

            var tasks = pools
                .Select(x => GetPoolForPostAsync(x.id, post.Id))
                .ToList();
            await Task.WhenAll(tasks);
            return [.. tasks.Select(x => x.Result)];
        }
    }
}
