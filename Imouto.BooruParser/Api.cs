﻿using System.Diagnostics.CodeAnalysis;
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

        return loader.FavoritePostAsync(postId.ToString());
    }

    public static int GetIntId(this PostIdentity postIdentity)
    {
        if (int.TryParse(postIdentity.Id, out var intId))
            return intId;

        throw new InvalidOperationException("This is probably is sankaku post, which doesn't support int id");
    }
}

public interface IBooruApiLoader
{
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

public interface IBooruApiAccessor
{
    Task FavoritePostAsync(string postId);
}

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
    PostIdentity? parentId = null) : IArtworkInfo, ISingleImage
{
    string IIdentityInfo.Id => Id.Id;

    public string Platform => Id.Platform;

    ILookup<ITagCategory, ITag> IArtworkInfo.Tags => Tags.ToLookup(t => t.Category, ITag (t) => t);

    [field: AllowNull, MaybeNull]
    IReadOnlyList<IImageFrame> IArtworkInfo.Thumbnails => field ??= ((Func<IReadOnlyList<IImageFrame>>)(() =>
    {
        var temp = new List<IImageFrame>();
        if (SampleUrl is not null)
            temp.Add(new ImageFrame { ImageUri = new(SampleUrl) });
        if (PreviewUrl is not null)
            temp.Add(new ImageFrame { ImageUri = new(PreviewUrl) });
        return temp;
    }))();

    IReadOnlyDictionary<string, object> IArtworkInfo.AdditionalInfo => new Dictionary<string, object>();

    /// <summary>
    /// TODO: 是否会有gif格式的图片？
    /// </summary>
    ImageType IArtworkInfo.ImageType => UgoiraFrameDelays is null
        ? ChildrenIds is null 
            ? ImageType.SingleImage
            : ImageType.ImageSet
        : ImageType.SingleAnimatedImage;

    int IArtworkInfo.TotalFavorite => -1;

    int IArtworkInfo.TotalView => -1;

    bool IArtworkInfo.IsFavorite => false;

    string IArtworkInfo.Description => "";

    public Uri WebsiteUri => new(Id.PlatformType switch
    {
        PlatformType.Danbooru => "https://danbooru.donmai.us/posts/" + Id.Id,
        PlatformType.Gelbooru => "https://gelbooru.com/index.php?page=post&s=view&id=" + Id.Id,
        PlatformType.Sankaku => "https://chan.sankakucomplex.com/posts/show/" + Id.Id,
        PlatformType.Yandere => "https://yande.re/post/show/" + Id.Id,
        PlatformType.Rule34 => "https://rule34.xxx/index.php?page=post&s=view&id=" + Id.Id,
        _ => throw new ArgumentOutOfRangeException(nameof(Id.PlatformType))
    });

    public DateTimeOffset UpdateDate => CreateDate;

    DateTimeOffset IArtworkInfo.ModifyDate => CreateDate;

    IPreloadableEnumerable<IUser> IArtworkInfo.Authors => [];

    IPreloadableEnumerable<IUser> IArtworkInfo.Uploaders => [Uploader];

    string IArtworkInfo.Title => "";

    int IImageSize.Width => FileResolution.Width;

    int IImageSize.Height => FileResolution.Height;

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

    public Func<Post, Task<IReadOnlyList<PostIdentity>>>? ChildrenIdsGetter { get => SwapReturn(ref field); init; }

    public Func<Post, Task<IReadOnlyList<Pool>>>? PoolsGetter { get => SwapReturn(ref field); init; }

    public Func<Post, Task<IReadOnlyList<Note>>>? NotesGetter { get => SwapReturn(ref field); init; }

    private static T? SwapReturn<T>(ref T? field) where T : class
    {
        var temp = field;
        field = null;
        return temp;
    }
}

public enum ExistState { Exist, MarkDeleted, Deleted }

public enum PopularType { Day, Week, Month }

public record Pool(string Id, string Name, int Position);

public record Note(string Id, string Text, Position Point, Size Size);

public record Tag(string Type, string Name) : ITag
{
    public ITagCategory Category => new TagCategory(Name);

    public string Description => "";
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

    public IReadOnlyList<IImageFrame> Avatar => [];

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
