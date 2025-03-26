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

public record SearchResult(IReadOnlyCollection<PostPreview> Results, string SearchTags, int PageNumber);

public record HistorySearchResult<T>(
    IReadOnlyCollection<T> Results,
    SearchToken? NextToken);

public record PostPreview(string Id, string? Md5Hash, string Title, bool IsBanned, bool IsDeleted);

/// <summary>
/// OriginalUrl, SampleUrl and PostIdentity.Md5 are nulls when post is banned
/// </summary>
public record Post(
    PostIdentity Id,
    string OriginalUrl,
    string SampleUrl,
    string PreviewUrl,
    ExistState ExistState,
    DateTimeOffset PostedAt,
    Uploader UploaderId,
    string? Source,
    Size FileResolution,
    int FileSizeInBytes,
    SafeRating SafeRating,
    IReadOnlyCollection<int> UgoiraFrameDelays,
    PostIdentity? Parent,
    IReadOnlyCollection<PostIdentity> ChildrenIds,
    IReadOnlyCollection<Pool> Pools,
    IReadOnlyCollection<Tag> Tags,
    IReadOnlyCollection<Note> Notes) : IArtworkInfo
{
    string IIdentityInfo.Id => Id.Id;

    public string Platform => Id.Platform;

    ILookup<ITagCategory, ITag> IArtworkInfo.Tags => Tags.ToLookup(t => t.Category, ITag (t) => t);

    IReadOnlyList<IImageFrame> IArtworkInfo.Thumbnails => throw new NotImplementedException();

    IReadOnlyDictionary<string, object> IArtworkInfo.AdditionalInfo => throw new NotImplementedException();

    ImageType IArtworkInfo.ImageType => throw new NotImplementedException();

    int IArtworkInfo.TotalFavorite => -1;

    int IArtworkInfo.TotalView => -1;

    bool IArtworkInfo.IsFavorite => false;

    string IArtworkInfo.Description => throw new NotImplementedException();

    public Uri WebsiteUri => new Uri(OriginalUrl);

    DateTimeOffset IArtworkInfo.CreateDate => throw new NotImplementedException();

    DateTimeOffset IArtworkInfo.UpdateDate => throw new NotImplementedException();

    DateTimeOffset IArtworkInfo.ModifyDate => throw new NotImplementedException();

    IPreloadableEnumerable<IUser> IArtworkInfo.Authors => throw new NotImplementedException();

    IPreloadableEnumerable<IUser> IArtworkInfo.Uploaders => throw new NotImplementedException();

    string IArtworkInfo.Title => "";

    
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

public record PostIdentity : IIdentityInfo
{
    public PostIdentity(string id, string md5Hash, PlatformType platform)
    {
        Id = id;
        Md5Hash = md5Hash;
        Platform = platform switch
        {
            PlatformType.Danbooru => IIdentityInfo.Danbooru,
            PlatformType.Gelbooru => IIdentityInfo.Gelbooru,
            PlatformType.Sankaku => IIdentityInfo.Sankaku,
            PlatformType.Yandere => IIdentityInfo.Yandere,
            PlatformType.Rule34 => IIdentityInfo.Rule34,
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
    }

    public string Platform { get; init; }

    public string Id { get; init; }

    public string Md5Hash { get; init; }

    public enum PlatformType
    {
        Danbooru,
        Gelbooru,
        Sankaku,
        Yandere,
        Rule34
    }
}

public record Uploader(string Id, string Name);

public record struct Position(int Top, int Left);

public record struct Size(int Width, int Height);

public record TagHistoryEntry(
    int HistoryId,
    DateTimeOffset UpdatedAt,
    string PostId,
    string? ParentId,
    bool ParentChanged);

public record NoteHistoryEntry(int HistoryId, string PostId, DateTimeOffset UpdatedAt);
