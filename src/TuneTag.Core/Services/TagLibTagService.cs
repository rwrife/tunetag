using TagLib;
using TuneTag.Core.Formats;
using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public sealed class TagLibTagService : ITagReader, ITagWriter, IArtService
{
    private static readonly string[] DefaultSupportedExtensions =
    [
        ".mp3",
        ".flac",
        ".ogg",
        ".m4a",
        ".mp4",
        ".m4b",
        ".aac",
        ".alac"
    ];

    private readonly FormatRouter _formatRouter;

    public TagLibTagService(FormatRouter formatRouter)
    {
        _formatRouter = formatRouter ?? throw new ArgumentNullException(nameof(formatRouter));
    }

    public TrackTags Read(string filePath)
    {
        var resolvedFormat = _formatRouter.Resolve(filePath);

        using var tagFile = TagLib.File.Create(filePath);
        var tag = tagFile.Tag;

        var trackTags = new TrackTags
        {
            Title = tag.Title,
            Artist = FirstOrNull(tag.Performers),
            Album = tag.Album,
            AlbumArtist = FirstOrNull(tag.AlbumArtists),
            TrackNumber = NormalizeUnsigned(tag.Track),
            DiscNumber = NormalizeUnsigned(tag.Disc),
            Year = NormalizeUnsigned(tag.Year),
            Genre = FirstOrNull(tag.Genres),
            Composer = FirstOrNull(tag.Composers),
            Comment = tag.Comment
        };

        trackTags.RawFields["format"] = resolvedFormat.Name;
        trackTags.RawFields["tag_types"] = tagFile.TagTypes.ToString();
        trackTags.RawFields["performers"] = string.Join(';', tag.Performers ?? []);
        trackTags.RawFields["album_artists"] = string.Join(';', tag.AlbumArtists ?? []);
        trackTags.RawFields["genres"] = string.Join(';', tag.Genres ?? []);
        trackTags.RawFields["composers"] = string.Join(';', tag.Composers ?? []);

        foreach (var picture in tag.Pictures ?? [])
        {
            var bytes = picture.Data?.Data ?? [];
            trackTags.AlbumArt.Add(new AlbumArt(
                string.IsNullOrWhiteSpace(picture.MimeType) ? "application/octet-stream" : picture.MimeType,
                bytes,
                MapToAlbumArtKind(picture.Type),
                picture.Description));
        }

        return trackTags;
    }

    public AlbumArt? ReadPrimary(string filePath)
    {
        _formatRouter.Resolve(filePath);

        using var tagFile = TagLib.File.Create(filePath);
        var picture = SelectPrimaryPicture(tagFile.Tag.Pictures);
        if (picture is null)
        {
            return null;
        }

        var bytes = picture.Data?.Data ?? [];
        var mimeType = string.IsNullOrWhiteSpace(picture.MimeType)
            ? "application/octet-stream"
            : picture.MimeType;

        return new AlbumArt(mimeType, bytes, MapToAlbumArtKind(picture.Type), picture.Description);
    }

    public void SetPrimary(string filePath, AlbumArt art)
    {
        ArgumentNullException.ThrowIfNull(art);
        _formatRouter.Resolve(filePath);

        using var tagFile = TagLib.File.Create(filePath);
        tagFile.Tag.Pictures = [CreatePicture(art)];
        tagFile.Save();
    }

    public void Remove(string filePath)
    {
        _formatRouter.Resolve(filePath);

        using var tagFile = TagLib.File.Create(filePath);
        tagFile.Tag.Pictures = [];
        tagFile.Save();
    }

    public string ExtractPrimary(string filePath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var art = ReadPrimary(filePath)
            ?? throw new InvalidOperationException($"No embedded artwork found in '{filePath}'.");

        var extension = MapMimeTypeToExtension(art.MimeType);
        var fullOutputPath = Path.ChangeExtension(outputPath, extension.TrimStart('.'));
        var directoryPath = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        System.IO.File.WriteAllBytes(fullOutputPath, art.Bytes);
        return fullOutputPath;
    }

    public int ApplyPrimaryToFolder(string folderPath, AlbumArt art, IEnumerable<string>? supportedExtensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(art);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder does not exist: {folderPath}");
        }

        var normalizedExtensions = new HashSet<string>(
            (supportedExtensions ?? DefaultSupportedExtensions).Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);

        var filePaths = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(path => normalizedExtensions.Contains(NormalizeExtension(Path.GetExtension(path))));

        var updatedCount = 0;
        foreach (var filePath in filePaths)
        {
            SetPrimary(filePath, art);
            updatedCount++;
        }

        return updatedCount;
    }

    public void Write(string filePath, TrackTags tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _formatRouter.Resolve(filePath);

        using var tagFile = TagLib.File.Create(filePath);
        var tag = tagFile.Tag;

        tag.Title = tags.Title;
        tag.Performers = ToSingleValueArray(tags.Artist);
        tag.Album = tags.Album;
        tag.AlbumArtists = ToSingleValueArray(tags.AlbumArtist);
        tag.Track = tags.TrackNumber ?? 0;
        tag.Disc = tags.DiscNumber ?? 0;
        tag.Year = tags.Year ?? 0;
        tag.Genres = ToSingleValueArray(tags.Genre);
        tag.Composers = ToSingleValueArray(tags.Composer);
        tag.Comment = tags.Comment;

        if (tags.AlbumArt.Count > 0)
        {
            tag.Pictures = tags.AlbumArt.Select(CreatePicture).ToArray();
        }

        tagFile.Save();
    }

    private static uint? NormalizeUnsigned(uint value)
    {
        return value == 0 ? null : value;
    }

    private static string? FirstOrNull(string[]? values)
    {
        if (values is null)
        {
            return null;
        }

        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }

    private static string[] ToSingleValueArray(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? [] : [value];
    }

    private static IPicture CreatePicture(AlbumArt art)
    {
        var picture = new Picture(new ByteVector(art.Bytes))
        {
            Type = MapToTagLibPictureType(art.Kind),
            MimeType = art.MimeType,
            Description = art.Description ?? string.Empty
        };

        return picture;
    }

    private static IPicture? SelectPrimaryPicture(IPicture[]? pictures)
    {
        if (pictures is null || pictures.Length == 0)
        {
            return null;
        }

        return pictures.FirstOrDefault(static picture => picture.Type == PictureType.FrontCover)
            ?? pictures[0];
    }

    private static string MapMimeTypeToExtension(string mimeType)
    {
        var normalized = mimeType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            _ => ".bin"
        };
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim();
        return normalized.StartsWith('.') ? normalized.ToLowerInvariant() : $".{normalized.ToLowerInvariant()}";
    }

    private static AlbumArtKind MapToAlbumArtKind(PictureType type)
    {
        return type switch
        {
            PictureType.FrontCover => AlbumArtKind.FrontCover,
            PictureType.BackCover => AlbumArtKind.BackCover,
            PictureType.LeafletPage => AlbumArtKind.Leaflet,
            PictureType.Media => AlbumArtKind.Media,
            PictureType.Artist => AlbumArtKind.Artist,
            PictureType.BandLogo => AlbumArtKind.BandLogo,
            PictureType.PublisherLogo => AlbumArtKind.PublisherLogo,
            _ => AlbumArtKind.Other
        };
    }

    private static PictureType MapToTagLibPictureType(AlbumArtKind kind)
    {
        return kind switch
        {
            AlbumArtKind.FrontCover => PictureType.FrontCover,
            AlbumArtKind.BackCover => PictureType.BackCover,
            AlbumArtKind.Leaflet => PictureType.LeafletPage,
            AlbumArtKind.Media => PictureType.Media,
            AlbumArtKind.Artist => PictureType.Artist,
            AlbumArtKind.BandLogo => PictureType.BandLogo,
            AlbumArtKind.PublisherLogo => PictureType.PublisherLogo,
            _ => PictureType.Other
        };
    }
}
