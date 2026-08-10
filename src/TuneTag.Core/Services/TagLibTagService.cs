using TagLib;
using TuneTag.Core.Formats;
using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public sealed class TagLibTagService : ITagReader, ITagWriter
{
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
