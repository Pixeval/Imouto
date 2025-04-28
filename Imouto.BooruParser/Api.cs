using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imouto.BooruParser.Implementations.Sankaku;
using Misaki;

namespace Imouto.BooruParser;

public static class BooruApiLoaderExtensions
{
    public static Task<Post> GetPostAsync(this IBooruApiLoader loader, int postId)
    {
        if (loader is SankakuApiLoader)
            throw new InvalidOperationException("Sankaku doesn't support getting post by int id");

        return loader.GetPostAsync(postId.ToString());
    }

    public static Task FavoritePostAsync(this IBooruApiAccessor loader, int postId)
    {
        if (loader is SankakuApiLoader)
            throw new InvalidOperationException("Sankaku doesn't support fav post by int id");

        return loader.PostFavoriteAsync(postId.ToString(), true);
    }

    public static int GetIntId(this PostIdentity postIdentity)
    {
        if (int.TryParse(postIdentity.Id, out var intId))
            return intId;

        throw new InvalidOperationException("This is probably is sankaku post, which doesn't support int id");
    }
}

public interface IBooruApiLoader : IGetArtworkService
{
    async Task<IArtworkInfo> IGetArtworkService.GetArtworkAsync(string id)
        => await GetPostAsync(id);

    Task<Post> GetPostAsync(string postId);

    Task<Post?> GetPostByMd5Async(string md5);

    Task<SearchResult> SearchAsync(string tags);

    Task<SearchResult> GetNextPageAsync(SearchResult results);

    Task<SearchResult> GetPreviousPageAsync(SearchResult results);

    Task<SearchResult> GetPopularPostsAsync(PopularType type);

    Task<HistorySearchResult<TagHistoryEntry>> GetTagHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default);

    Task<HistorySearchResult<NoteHistoryEntry>> GetNoteHistoryPageAsync(
        SearchToken? token,
        int limit = 100,
        CancellationToken ct = default);
}

public interface IBooruApiAccessor : IPostFavoriteService;

/// <param name="Page">For danbooru: b{lowest-history-id-on-current-page}</param>
public record SearchToken(string Page);

public record SearchResult(IReadOnlyList<PostPreview> Results, string SearchTags, int PageNumber);

public record HistorySearchResult<T>(
    IReadOnlyList<T> Results,
    SearchToken? NextToken);

public record PostPreview(string Id, string? Md5Hash, string Title, bool IsBanned, bool IsDeleted);

/// <summary>
/// OriginalUrl, SampleUrl and PostIdentity.Md5 are nulls when post is banned
/// </summary>
public class Post(
    PostIdentity id,
    string originalUrl,
    string? sampleUrl,
    string? previewUrl,
    ExistState existState,
    DateTimeOffset createDate,
    Uploader uploader,
    string? source,
    Size fileResolution,
    ulong byteSize,
    SafeRating safeRating,
    IReadOnlyList<Tag> tags,
    IReadOnlyList<Note>? notes,
    PostIdentity? parentId = null) : ISingleImage, ISerializable
{
    [JsonIgnore]
    string IIdentityInfo.Id => Id.Id;

    [JsonIgnore]
    string IPlatformInfo.Platform => Id.Platform;

    [JsonIgnore]
    ILookup<ITagCategory, ITag> IArtworkInfo.Tags => Tags.ToLookup(t => t.Category, ITag (t) => t);

    [JsonIgnore]
    [field: AllowNull, MaybeNull]
    IReadOnlyCollection<IImageFrame> IArtworkInfo.Thumbnails => field ??= ((Func<IReadOnlyCollection<IImageFrame>>)(() =>
    {
        var temp = new List<IImageFrame>();
        if (PreviewUrl is not null)
            temp.Add(new ImageFrame(
                IImageSize.Uniform(this, id.PlatformType switch
                {
                    PlatformType.Danbooru => 180,
                    PlatformType.Gelbooru or PlatformType.Rule34 => 250,
                    PlatformType.Yandere => 300,
                    PlatformType.Sankaku => 600
                })) { ImageUri = new Uri(PreviewUrl) });
        if (SampleUrl is not null)
            temp.Add(new ImageFrame(
                id.PlatformType is PlatformType.Yandere
                    ? IImageSize.Uniform(this, 1500)
                    : IImageSize.FixWidth(this, id.PlatformType switch
                    {
                        PlatformType.Danbooru or PlatformType.Gelbooru or PlatformType.Rule34 => 850,
                        PlatformType.Sankaku => 2000
                    })) { ImageUri = new Uri(SampleUrl) });
        return temp;
    }))();

    [JsonIgnore]
    IReadOnlyDictionary<string, object> IArtworkInfo.AdditionalInfo => new Dictionary<string, object>();

    /// <summary>
    /// TODO: 是否会有gif格式的图片？实现IImageSet
    /// </summary>
    [JsonIgnore]
    ImageType IArtworkInfo.ImageType => ImageType.SingleImage;
    /*
    UgoiraFrameDelays is null
        ? ChildrenIds is null
            ? ImageType.SingleImage
            : ImageType.ImageSet
        : ImageType.SingleAnimatedImage;
    */

    [JsonIgnore]
    int IArtworkInfo.TotalFavorite => -1;

    [JsonIgnore]
    int IArtworkInfo.TotalView => -1;

    [JsonIgnore]
    bool IArtworkInfo.IsFavorite => false;

    [JsonIgnore]
    public bool IsAiGenerated => false;

    [JsonIgnore]
    string IArtworkInfo.Description => "";

    [JsonIgnore]
    public Uri WebsiteUri => new(Id.PlatformType switch
    {
        PlatformType.Danbooru => "https://danbooru.donmai.us/posts/" + Id.Id,
        PlatformType.Gelbooru => "https://gelbooru.com/index.php?page=post&s=view&id=" + Id.Id,
        PlatformType.Sankaku => "https://chan.sankakucomplex.com/posts/show/" + Id.Id,
        PlatformType.Yandere => "https://yande.re/post/show/" + Id.Id,
        PlatformType.Rule34 => "https://rule34.xxx/index.php?page=post&s=view&id=" + Id.Id,
        _ => throw new ArgumentOutOfRangeException(nameof(Id.PlatformType))
    });

    [JsonIgnore]
    public Uri AppUri => new Uri($"pixeval://{Id.PlatformType}/{Id.Id}");

    [JsonIgnore]
    public DateTimeOffset UpdateDate => CreateDate;

    [JsonIgnore]
    DateTimeOffset IArtworkInfo.ModifyDate => CreateDate;

    [JsonIgnore]
    IPreloadableList<IUser> IArtworkInfo.Authors => [];

    [JsonIgnore]
    IPreloadableList<IUser> IArtworkInfo.Uploaders => [Uploader];

    [JsonIgnore]
    string IArtworkInfo.Title => "";

    [JsonIgnore]
    int IImageSize.Width => FileResolution.Width;

    [JsonIgnore]
    int IImageSize.Height => FileResolution.Height;

    [JsonIgnore]
    Uri IImageFrame.ImageUri => new(OriginalUrl);

    public PostIdentity Id { get; } = id;

    public string OriginalUrl { get; } = originalUrl;

    public string? SampleUrl { get; } = sampleUrl;

    public string? PreviewUrl { get; } = previewUrl;

    public ExistState ExistState { get; } = existState;

    public DateTimeOffset CreateDate { get; } = createDate;

    public Uploader Uploader { get; } = uploader;

    public string? Source { get; } = source;

    public Size FileResolution { get; } = fileResolution;

    public ulong ByteSize { get; } = byteSize;

    public SafeRating SafeRating { get; } = safeRating;

    public IReadOnlyList<Tag> Tags { get; } = tags;

    public PostIdentity? Parent { get; } = parentId;

    public IReadOnlyList<Note>? Notes { get; } = notes;

    public IReadOnlyList<int>? UgoiraFrameDelays { get; init; }

    public IReadOnlyList<PostIdentity>? ChildrenIds { get; init; } 

    public IReadOnlyList<Pool>? Pools { get; init; }

    [JsonIgnore]
    public Func<Post, Task<IReadOnlyList<PostIdentity>>>? ChildrenIdsGetter { get => SwapReturn(ref field); init; }

    [JsonIgnore]
    public Func<Post, Task<IReadOnlyList<Pool>>>? PoolsGetter { get => SwapReturn(ref field); init; }

    [JsonIgnore]
    public Func<Post, Task<IReadOnlyList<Note>>>? NotesGetter { get => SwapReturn(ref field); init; }

    private static T? SwapReturn<T>(ref T? field) where T : class
    {
        var temp = field;
        field = null;
        return temp;
    }

    [JsonIgnore]
    public int SetIndex => -1;

    public string Serialize() => JsonSerializer.Serialize(this, typeof(Post), PostJsonSerializerContext.Default);

    public static ISerializable Deserialize(string data) => (Post) JsonSerializer.Deserialize(data, typeof(Post), PostJsonSerializerContext.Default)!;

    [JsonIgnore]
    public string SerializeKey => typeof(Post).FullName!;
}

public enum ExistState { Exist, MarkDeleted, Deleted }

public enum PopularType { Day, Week, Month }

public record Pool(string Id, string Name, int Position);

public record Note(string Id, string Text, Position Point, Size Size);

public record Tag(string Type, string Name) : ITag
{
    public ITagCategory Category => new TagCategory(Type);

    public string Description => "";

    public string TranslatedName { get; init; } = "";
}

public record PostIdentity(string Id, string Md5Hash, PlatformType PlatformType) : IIdentityInfo
{
    public string Platform { get; } = PlatformType.GetString();

    public PostIdentity Fork(string id, string md5Hash) => new(id, md5Hash, PlatformType);

    public PostIdentity? TryFork(string? id, string? md5Hash)
    {
        if (id is null || md5Hash is null)
            return null;
        return new(id, md5Hash, PlatformType);
    }

    public PostIdentity? TryFork(long id, string? md5Hash)
    {
        if (id is 0 || md5Hash is null)
            return null;
        return new(id.ToString(), md5Hash, PlatformType);
    }

    public PostIdentity? TryFork(long? id, string? md5Hash)
    {
        if (id is { } i and not 0 && md5Hash is not null)
            return new(i.ToString(), md5Hash, PlatformType);
        return null;
    }
}

public enum PlatformType
{
    Danbooru,
    Gelbooru,
    Sankaku,
    Yandere,
    Rule34
}

public static class PlatformTypeHelper 
{
    public static string GetString(this PlatformType type) =>
        type switch
        {
            PlatformType.Danbooru => "danbooru",
            PlatformType.Gelbooru => "gelbooru",
            PlatformType.Sankaku => "sankaku",
            PlatformType.Yandere => "yandere",
            PlatformType.Rule34 => "rule34",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
}

public class Uploader(string id, string name, PlatformType platform) : IUser
{
    public string Platform { get; } = platform.GetString();

    public string Description => "";

    public Uri WebsiteUri { get; } = new(platform switch
    {
        PlatformType.Danbooru => "https://danbooru.donmai.us/users/" + id,
        PlatformType.Gelbooru => "https://gelbooru.com/index.php?page=account&s=profile&id=" + id,
        PlatformType.Sankaku => "https://chan.sankakucomplex.com/users/"+ name,
        PlatformType.Yandere => "https://yande.re/user/show/" + id,
        PlatformType.Rule34 => "https://rule34.xxx/index.php?page=account&s=profile&uname=" + name,
        _ => throw new ArgumentOutOfRangeException(nameof(platform))
    });

    public Uri AppUri => new("pixeval://empty");

    public IReadOnlyCollection<IImageFrame> Avatar => [];

    public IReadOnlyDictionary<string, Uri> ContactInformation => new Dictionary<string, Uri>();

    public IReadOnlyDictionary<string, object> AdditionalInfo => new Dictionary<string, object>();

    public string Id { get; } = id;

    public string Name { get; } = name;
}

public record struct Position(int Top, int Left);

public record struct Size(int Width, int Height);

public record TagHistoryEntry(
    long HistoryId,
    DateTimeOffset UpdatedAt,
    string PostId,
    string? ParentId,
    bool ParentChanged);

public record NoteHistoryEntry(long HistoryId, string PostId, DateTimeOffset UpdatedAt);

[JsonSerializable(typeof(Post))]
public partial class PostJsonSerializerContext : JsonSerializerContext;
