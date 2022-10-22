﻿namespace ImoutoRebirth.BooruParser;

public interface IBooruApiLoader
{
    Task<Post> GetPostAsync(int postId);

    Task<SearchResult> SearchAsync(string tags);

    Task<SearchResult> GetPopularPostsAsync(PopularType type);

    Task<HistorySearchResult<TagsHistoryEntry>> LoadTagHistoryPageAsync(SearchToken? token);

    Task<HistorySearchResult<NoteHistoryEntry>> LoadNotesHistoryPageAsync(SearchToken? token);
}

public interface IBooruApiAccessor
{
    Task FavoritePostAsync(int postId);
}

/// <param name="Page">For danbooru: b{lowest-history-id-on-current-page}</param>
public record SearchToken(string Page);

public record SearchResult(IReadOnlyCollection<PostPreview> Results, int? SearchCount)
{
    public bool IsFound => Results.Any();
}

public record HistorySearchResult<T>(
    IReadOnlyCollection<T> Results, 
    SearchToken NextToken)
{
    public bool IsFound => Results.Any();
}

public record PostPreview(int Id, string Md5Hash, string Title);

public record Post(
    PostIdentity Id,
    string OriginalUrl,
    string SampleUrl,
    ExistState ExistState,
    DateTimeOffset PostedAt,
    Uploader UploaderId,
    string Source,
    Size FileResolution,
    int FileSizeInBytes,
    Rating Rating,
    RatingSafeLevel RatingSafeLevel,
    IReadOnlyCollection<int> UgoiraFrameDelays,
    PostIdentity? Parent,
    IReadOnlyCollection<PostIdentity> ChildrenIds,
    IReadOnlyCollection<Pool> Pools,
    IReadOnlyCollection<Tag> Tags,
    IReadOnlyCollection<Note> Notes);


public enum ExistState { Exist, MarkDeleted, Deleted }

public enum PopularType { Day, Week, Month }

public enum Rating { Safe, Questionable, Explicit }

public enum RatingSafeLevel { None, Sensitive, General }

public record Pool(int Id, string Name, int Position);

public record Note(int Id, string Text, Position Point, Size Size);

public record Tag(string Type, string Name);

public record PostIdentity(int Id, string Md5Hash);

public record Uploader(int Id, string Name);

public record struct Position(int Top, int Left);

public record struct Size(int Width, int Height);

public record TagsHistoryEntry(
    int HistoryId,
    DateTimeOffset UpdatedAt,
    int PostId,
    string Username,
    Rating Rating,
    int? ParentId,
    bool ParentChanged,
    IReadOnlyCollection<Tag> AddedTags,
    IReadOnlyCollection<Tag> RemovedTags,
    IReadOnlyCollection<Tag> UnchangedTags);

public record NoteHistoryEntry(int PostId, DateTimeOffset UpdatedAt);
