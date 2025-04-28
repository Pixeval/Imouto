using Flurl.Http;
using Flurl.Http.Configuration;
using Imouto.BooruParser.Extensions;
using Microsoft.Extensions.Options;
using Misaki;

namespace Imouto.BooruParser.Implementations.Danbooru;

public class DanbooruApiLoader(IFlurlClientCache factory, IOptions<DanbooruSettings> options) : IBooruApiLoader, IBooruApiAccessor
{
    public string Platform => IPlatformInfo.Danbooru;
    private const string BaseUrl = "https://danbooru.donmai.us";
    private readonly IFlurlClient _flurlClient = factory.GetForDomain(new(BaseUrl)).BeforeCall(x => SetAuthParameters(x, options));
    private readonly string _botUserAgent = options.Value.BotUserAgent ?? "gdl/1.24.5";

    public async Task<Post> GetPostAsync(string postId)
    {
        var post = await _flurlClient.Request("posts", $"{postId}.json")
            .SetQueryParam("only", "id,tag_string_artist,tag_string_character,tag_string_copyright,pools,tag_string_general,tag_string_meta,parent_id,md5,file_url,large_file_url,preview_file_url,file_ext,last_noted_at,is_banned,is_deleted,created_at,uploader_id,source,image_width,image_height,file_size,rating,media_metadata[metadata],parent[id,md5],children[id,md5],notes[id,x,y,width,height,body],uploader[id,name]")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<DanbooruPost>();

        var postIdentity = new PostIdentity(postId, post.Md5, PlatformType.Danbooru);
        return new(
            postIdentity,
            post.FileUrl,
            post.LargeFileUrl,
            post.PreviewFileUrl,
            post.IsBanned || post.IsDeleted ? ExistState.MarkDeleted : ExistState.Exist,
            post.CreatedAt.ToUniversalTime(),
            new(post.UploaderId.ToString(), post.Uploader.Name.Replace('_', ' '), PlatformType.Danbooru),
            post.Source,
            new(post.ImageWidth, post.ImageHeight),
            post.FileSize,
            SafeRating.Parse(post.Rating),
            GetTags(post),
            GetNotes(post),
            postIdentity.TryFork(post.ParentId, post.Parent?.Md5))
        {
            UgoiraFrameDelays = GetUgoiraMetadata(post),
            ChildrenIds = GetChildren(post),
            PoolsGetter = GetPoolsAsync,
        };
    }

    public async Task<Post?> GetPostByMd5Async(string md5)
    {
        var posts = await _flurlClient.Request("posts.json")
            .SetQueryParam("only", "id,tag_string_artist,tag_string_character,tag_string_copyright,pools,tag_string_general,tag_string_meta,parent_id,md5,file_url,large_file_url,preview_file_url,file_ext,last_noted_at,is_banned,is_deleted,created_at,uploader_id,source,image_width,image_height,file_size,rating,media_metadata[metadata],parent[id,md5],children[id,md5],notes[id,x,y,width,height,body],uploader[id,name]")
            .SetQueryParam("tags", $"md5:{md5}")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruPost>>();

        if (posts.Count is 0)
            return null;

        var post = posts[0];
        var postIdentity = new PostIdentity(post.Id.ToString(), post.Md5, PlatformType.Danbooru);
        return new(
            postIdentity,
            post.FileUrl,
            post.LargeFileUrl,
            post.PreviewFileUrl,
            post.IsBanned || post.IsDeleted ? ExistState.MarkDeleted : ExistState.Exist,
            post.CreatedAt,
            new(post.UploaderId.ToString(), post.Uploader.Name.Replace('_', ' '), PlatformType.Danbooru),
            post.Source,
            new(post.ImageWidth, post.ImageHeight),
            post.FileSize,
            SafeRating.Parse(post.Rating),
            GetTags(post),
            GetNotes(post),
            postIdentity.TryFork(post.ParentId, post.Parent?.Md5))
        {
            UgoiraFrameDelays = GetUgoiraMetadata(post),
            ChildrenIds = GetChildren(post),
            PoolsGetter = GetPoolsAsync,
        };
    }

    public async Task<SearchResult> SearchAsync(string tags)
    {
        var posts = await _flurlClient.Request("posts.json")
            .SetQueryParam("tags", tags)
            .SetQueryParam("page", 1)
            .SetQueryParam("only", "id,md5,tag_string,is_banned,is_deleted")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruPostPreview>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.TagString, x.IsBanned, x.IsDeleted))], tags, 1);
    }

    public async Task<SearchResult> GetNextPageAsync(SearchResult results)
    {
        var nextPage = results.PageNumber + 1;

        var posts = await _flurlClient.Request("posts.json")
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("page", nextPage)
            .SetQueryParam("only", "id,md5,tag_string,is_banned,is_deleted")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruPostPreview>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.TagString, x.IsBanned, x.IsDeleted))], results.SearchTags, nextPage);
    }

    public async Task<SearchResult> GetPreviousPageAsync(SearchResult results)
    {
        if (results.PageNumber <= 1)
            throw new ArgumentOutOfRangeException(nameof(results.PageNumber), results.PageNumber, null);

        var nextPage = results.PageNumber - 1;

        var posts = await _flurlClient.Request("posts.json")
            .SetQueryParam("tags", results.SearchTags)
            .SetQueryParam("page", nextPage)
            .SetQueryParam("only", "id,md5,tag_string,is_banned,is_deleted")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruPostPreview>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.TagString, x.IsBanned, x.IsDeleted))], results.SearchTags, nextPage);
    }

    public async Task<SearchResult> GetPopularPostsAsync(PopularType type)
    {
        var scale = type switch
        {
            PopularType.Day => "day",
            PopularType.Week => "week",
            PopularType.Month => "month",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
        
        var posts = await _flurlClient.Request("explore", "posts", "popular.json")
            .SetQueryParam("date", $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffzzz}")
            .SetQueryParam("scale", scale)
            .SetQueryParam("only", "id,md5,tag_string,is_banned,is_deleted")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruPostPreview>>();

        return new([.. posts.Select(x => new PostPreview(x.Id.ToString(), x.Md5, x.TagString, x.IsBanned, x.IsDeleted))], "popular",1);
    }

    public async Task<HistorySearchResult<TagHistoryEntry>> GetTagHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
    {
        var request = _flurlClient.Request("post_versions.json")
            .SetQueryParam("limit", limit);
        
        if (token != null)
            request = request.SetQueryParam("page", token.Page);

        var found = await request
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruTagsHistoryEntry>>(cancellationToken: ct);

        if (found.Count is 0)
            return new([], null);

        var entries = found
            .Select(
                x =>
                    new TagHistoryEntry(
                        x.Id,
                        x.UpdatedAt,
                        x.PostId.ToString(),
                        x.ParentId?.ToString(),
                        x.ParentChanged))
            .ToArray();

        var nextPage = token?.Page[0] switch
        {
            null => $"b{found.Min(x => x.Id)}",
            'b' => $"b{found.Min(x => x.Id)}",
            'a' => $"a{found.Max(x => x.Id)}",
            var x when int.TryParse(x.ToString(), out var page) => (page + 1).ToString(),
            _ => "2"
        };

        return new(entries, new(nextPage));
    }

    public async Task<HistorySearchResult<NoteHistoryEntry>> GetNoteHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default)
    {
        var request = _flurlClient.Request("note_versions.json")
            .SetQueryParam("limit", limit);
        
        if (token is not null)
            request = request.SetQueryParam("page", token.Page);

        var found = await request
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruNotesHistoryEntry>>(cancellationToken: ct);

        if (found.Count is 0)
            return new([], null);
        
        var entries = found
            .Select(x => new NoteHistoryEntry(x.Id, x.PostId.ToString(), x.UpdatedAt))
            .ToList();

        var nextPage = token?.Page[0] switch
        {
            null => $"b{found.Min(x => x.Id)}",
            'b' => $"b{found.Min(x => x.Id)}",
            'a' => $"a{found.Max(x => x.Id)}",
            var _ when int.TryParse(token.Page, out var page) => (page + 1).ToString(),
            _ => "2"
        };

        return new(entries, new(nextPage));
    }

    public async Task<bool> PostFavoriteAsync(string postId, bool favorite)
    {
        if (!favorite)
            throw new NotSupportedException(favorite.ToString());
        await _flurlClient.Request("favorites.json")
            .SetQueryParam("post_id", postId)
            .WithUserAgent(_botUserAgent)
            .PostAsync();
        return true;
    }

    private static IReadOnlyList<PostIdentity> GetChildren(DanbooruPost post)
        => [.. post.Children.Select(x => new PostIdentity(x.Id.ToString(), x.Md5, PlatformType.Danbooru))];

    private static IReadOnlyList<int>? GetUgoiraMetadata(DanbooruPost post)
    {
        return post.FileExt is "zip" ? post.MediaMetadata.Metadata.UgoiraFrameDelays : null;
    }

    private async Task<IReadOnlyList<Pool>> GetPoolsAsync(Post post)
    {
        var postId = post.Id.Id;
        var pools = await _flurlClient.Request("pools.json")
            .SetQueryParam("search[post_tags_match]", $"id:{postId}")
            .SetQueryParam("only", "id,name,post_ids")
            .WithUserAgent(_botUserAgent)
            .GetJsonAsync<IReadOnlyList<DanbooruPool>>();

        var pId = long.Parse(postId);

        return [.. pools.Select(x => new Pool(x.Id.ToString(), x.Name, Array.IndexOf(x.PostIds, pId)))];
    }

    private static IReadOnlyList<Note> GetNotes(DanbooruPost post)
    {
        if (post.LastNotedAt is null)
            return [];

        return [.. post.Notes.Select(x => new Note(x.Id.ToString(), x.Body, new(x.Y, x.X), new(x.Width, x.Height)))];
    }

    private static IReadOnlyList<Tag> GetTags(DanbooruPost post)
        => [.. post.TagStringArtist.Split(' ').Select(x => (Type: "artist", Tag: x))
            .Union(post.TagStringCharacter.Split(' ').Select(x => (Type: "character", Tag: x)))
            .Union(post.TagStringCopyright.Split(' ').Select(x => (Type: "copyright", Tag: x)))
            .Union(post.TagStringGeneral.Split(' ').Select(x => (Type: "general", Tag: x)))
            .Union(post.TagStringMeta.Split(' ').Select(x => (Type: "meta", Tag: x)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Tag))
            .Select(x => new Tag(x.Type, x.Tag.Replace('_', ' ')))];

    private static async Task SetAuthParameters(FlurlCall call, IOptions<DanbooruSettings> options)
    {
        var login = options.Value.Login;
        var apiKey = options.Value.ApiKey;
        var delay = options.Value.PauseBetweenRequests;

        if (options.Value is { Login: not null, ApiKey: not null })
            call.Request.SetQueryParam("login", login).SetQueryParam("api_key", apiKey);

        if (delay > TimeSpan.Zero)
            await Throttler.Get("danbooru").UseAsync(delay);
    }
}
