namespace TuneTag.Core.Models;

public sealed class AlbumArt
{
    public AlbumArt(string mimeType, byte[] bytes, AlbumArtKind kind = AlbumArtKind.FrontCover, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentNullException.ThrowIfNull(bytes);

        MimeType = mimeType;
        Bytes = bytes.ToArray();
        Kind = kind;
        Description = description;
    }

    public string MimeType { get; }

    public byte[] Bytes { get; }

    public AlbumArtKind Kind { get; }

    public string? Description { get; }
}
