using System.Xml.Linq;
using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;
using Imouto.BooruParser.Extensions;
using Microsoft.Extensions.Options;
using Misaki;

namespace Imouto.BooruParser.Implementations.Rule34;

public class Rule34ApiLoader : IBooruApiLoader
{
    public string Platform => IPlatformInfo.Rule34;

    private const string JsonBaseUrl = "https://api.rule34.xxx";

    private readonly IOptions<Rule34Settings> _options;
    private readonly IFlurlClient _flurlJsonClient;

    public Rule34ApiLoader(IFlurlClientCache factory, IOptions<Rule34Settings> options)
    {
        _options = options;
        var botUserAgent = options.Value.BotUserAgent;
        if (string.IsNullOrWhiteSpace(botUserAgent))
            throw new InvalidOperationException("BotUserAgent is required to make Rule34 API calls");

        _flurlJsonClient = factory.GetForDomain(new Url(JsonBaseUrl))
            .WithHeader("User-Agent", botUserAgent)
            .WithHeader("Accept", "application/json, application/xml;q=0.9")
            .BeforeCall(_ => DelayWithThrottler(options));
    }

    public async Task<Post> GetPostAsync(string postId)
    {
        // https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&id=
        var postJson = await Request()
            .SetQueryParams(new
            {
                page = "dapi",
                s = "post",
                q = "index",
                json = 1,
                limit = 1,
                id = postId,
                fields = "tag_info"
            })
            .GetJsonAsync<Rule34Post[]>();
        var post = postJson?.FirstOrDefault();

        if (post is null)
            throw new PostNotFoundException("Rule34", postId);

        return CreatePost(post, await GetNotesAsync(post));
    }

    public async Task<Post?> GetPostByMd5Async(string md5)
    {
        // https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&tags=md5:
        var postJson = await Request()
            .SetQueryParams(new
            {
                page = "dapi",
                s = "post",
                q = "index",
                json = 1,
                limit = 1,
                tags = $"md5:{md5}",
                fields = "tag_info"
            })
            .GetJsonAsync<Rule34Post[]>();

        var post = postJson?.FirstOrDefault();

        return post != null
            ? CreatePost(post, await GetNotesAsync(post))
            : null;
    }

    public async Task<SearchResult> SearchAsync(string tags)
    {
        // https://api.rule34.xxx/index.php?page=dapi&s=post&q=index&json=1&tags=1girl
        var postJson = await Request()
            .SetQueryParam("page", "dapi")
            .SetQueryParam("s", "post")
            .SetQueryParam("q", "index")
            .SetQueryParam("json", 1)
            .SetQueryParam("limit", 20)
            .SetQueryParam("tags", tags)
            .SetQueryParam("pid", 0)
            .GetJsonAsync<Rule34Post[]>();

        return new(postJson?
            .Select(x => new PostPreview(x.Id.ToString(), x.Hash, x.Tags, false, false))
            .ToArray() ?? [], tags, 0);
    }

    public async Task<SearchResult> GetNextPageAsync(SearchResult results)
    {
        var nextPage = results.PageNumber + 1;

        var postJson = await Request()
            .SetQueryParam("page", "dapi")
            .SetQueryParam("s", "post")
            .SetQueryParam("q", "index")
            .SetQueryParam("json", 1)
            .SetQueryParam("limit", 20)
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("pid", nextPage)
            .GetJsonAsync<Rule34Post[]>();

        return new(postJson?
            .Select(x => new PostPreview(
                x.Id.ToString(),
                x.Hash,
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

        var postJson = await Request()
            .SetQueryParam("page", "dapi")
            .SetQueryParam("s", "post")
            .SetQueryParam("q", "index")
            .SetQueryParam("json", 1)
            .SetQueryParam("limit", 20)
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("pid", nextPage)
            .GetJsonAsync<Rule34Post[]>();

        return new(postJson?
            .Select(x => new PostPreview(
                x.Id.ToString(),
                x.Hash,
                x.Tags,
                false,
                false))
            .ToArray() ?? [], results.SearchTags, nextPage);
    }

    public Task<SearchResult> GetPopularPostsAsync(PopularType type)
        => throw new NotSupportedException("Rule34 does not support popularity charts");

    public Task<HistorySearchResult<TagHistoryEntry>> GetTagHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
        => throw new NotSupportedException("Rule34 does not support history");

    public Task<HistorySearchResult<NoteHistoryEntry>> GetNoteHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
        => throw new NotSupportedException("Rule34 does not support history");

    private static PostIdentity? GetParent(Rule34Post post)
        => post.ParentId is not 0 ? new PostIdentity(post.ParentId.ToString(), string.Empty, PlatformType.Rule34) : null;

    private IFlurlRequest Request()
        => _flurlJsonClient.Request("index.php")
            .AppendQueryParam("api_key", _options.Value.ApiKey)
            .AppendQueryParam("user_id", _options.Value.UserId);

    private async Task<IReadOnlyList<Note>> GetNotesAsync(Rule34Post post)
    {
        if (post.HasNotes != true)
            return [];

        var notesXml = await Request()
            .SetQueryParams(new
            {
                page = "dapi",
                s = "note",
                q = "index",
                post_id = post.Id
            })
            .GetStringAsync();

        return XDocument.Parse(notesXml).Root?
            .Elements("note")
            .Where(x => !string.Equals(
                (string?) x.Attribute("is_active"),
                "false",
                StringComparison.OrdinalIgnoreCase))
            .Select(x => new
            {
                Id = (int) x.Attribute("id")!,
                Text = (string?) x.Attribute("body") ?? string.Empty,
                X = (int) x.Attribute("x")!,
                Y = (int) x.Attribute("y")!,
                Width = (int) x.Attribute("width")!,
                Height = (int) x.Attribute("height")!
            })
            .OrderBy(x => x.Id)
            .Select(x => new Note(
                x.Id.ToString(),
                x.Text,
                new Position(x.Y, x.X),
                new Size(x.Width, x.Height)))
            .ToArray() ?? [];
    }

    private static IReadOnlyList<Tag> GetTags(Rule34Post post)
        =>
        [
            .. post.TagInfo?
                   .Select(x => new Tag(
                       x.Type == "tag" ? "general" : x.Type,
                       x.Name.Replace('_', ' ')))
               ?? post.Tags
                   .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(x => new Tag("general", x.Replace('_', ' ')))
        ];

    private static async Task DelayWithThrottler(IOptions<Rule34Settings> options)
    {
        var delay = options.Value.PauseBetweenRequests;
        if (delay > TimeSpan.Zero)
            await Throttler.Get("rule34").UseAsync(delay);
    }

    private static Post CreatePost(Rule34Post post, IReadOnlyList<Note> notes)
    {
        var postIdentity = new PostIdentity(post.Id.ToString(), post.Hash, PlatformType.Rule34);
        return new(
            postIdentity,
            post.FileUrl,
            string.IsNullOrWhiteSpace(post.SampleUrl) ? null : post.SampleUrl,
            post.PreviewUrl,
            ExistState.Exist,
            DateTimeOffset.FromUnixTimeSeconds(post.Change),
            new("-1", post.Owner.Replace('_', ' '), PlatformType.Rule34),
            post.Source,
            new(post.Width, post.Height),
            0,
            SafeRating.Parse(post.Rating),
            GetTags(post),
            notes,
            GetParent(post));
    }
}
