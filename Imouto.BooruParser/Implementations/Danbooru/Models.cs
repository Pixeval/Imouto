using System.Text.Json.Serialization;

namespace Imouto.BooruParser.Implementations.Danbooru;

public record DanbooruChild(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("md5")] string Md5
);

public record DanbooruMediaMetadata(
    [property: JsonPropertyName("metadata")] DanbooruMetadata Metadata
);

public record DanbooruMetadata(
    [property: JsonPropertyName("Ugoira:FrameDelays")] IReadOnlyList<int>? UgoiraFrameDelays
);

public record DanbooruNote(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("body")] string Body
);

public record DanbooruParent(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("md5")] string Md5
);

/// <summary>
/// only=id,md5,tag_string,is_banned,is_deleted
/// </summary>
public record DanbooruPostPreview(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("md5")] string? Md5,
    [property: JsonPropertyName("tag_string")] string TagString,
    [property: JsonPropertyName("is_banned")] bool IsBanned,
    [property: JsonPropertyName("is_deleted")] bool IsDeleted
);

public record DanbooruPost(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("last_noted_at")] DateTimeOffset? LastNotedAt,
    [property: JsonPropertyName("uploader_id")] long UploaderId,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("file_ext")] string FileExt,
    [property: JsonPropertyName("md5")] string Md5,
    [property: JsonPropertyName("rating")] string Rating,
    [property: JsonPropertyName("image_width")] int ImageWidth,
    [property: JsonPropertyName("image_height")] int ImageHeight,
    [property: JsonPropertyName("parent_id")] long? ParentId,
    [property: JsonPropertyName("file_size")] ulong FileSize,
    [property: JsonPropertyName("is_deleted")] bool IsDeleted,
    [property: JsonPropertyName("is_banned")] bool IsBanned,
    [property: JsonPropertyName("file_url")] string FileUrl,
    [property: JsonPropertyName("large_file_url")] string? LargeFileUrl,
    [property: JsonPropertyName("preview_file_url")] string PreviewFileUrl,
    [property: JsonPropertyName("media_metadata")] DanbooruMediaMetadata MediaMetadata,
    [property: JsonPropertyName("children")] IReadOnlyList<DanbooruChild> Children,
    [property: JsonPropertyName("parent")] DanbooruParent? Parent,
    [property: JsonPropertyName("notes")] IReadOnlyList<DanbooruNote> Notes,
    [property: JsonPropertyName("uploader")] DanbooruUploader Uploader,
    [property: JsonPropertyName("tag_string_artist")] string TagStringArtist,
    [property: JsonPropertyName("tag_string_character")] string TagStringCharacter,
    [property: JsonPropertyName("tag_string_copyright")] string TagStringCopyright,
    [property: JsonPropertyName("tag_string_general")] string TagStringGeneral,
    [property: JsonPropertyName("tag_string_meta")] string TagStringMeta
);

public record DanbooruUploader(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name
);

public record DanbooruPool(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("post_ids")] long[] PostIds
);

public record DanbooruTagsHistoryEntry(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("post_id")] int PostId,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("parent_id")] int? ParentId,
    [property: JsonPropertyName("parent_changed")] bool ParentChanged
);

public record DanbooruNotesHistoryEntry(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("post_id")] long PostId,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt
);
