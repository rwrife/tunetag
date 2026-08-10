namespace TuneTag.Core.Models;

public sealed class TrackTags
{
    public string? Title { get; set; }

    public string? Artist { get; set; }

    public string? Album { get; set; }

    public string? AlbumArtist { get; set; }

    public uint? TrackNumber { get; set; }

    public uint? DiscNumber { get; set; }

    public uint? Year { get; set; }

    public string? Genre { get; set; }

    public string? Composer { get; set; }

    public string? Comment { get; set; }

    public IDictionary<string, string?> RawFields { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public IList<AlbumArt> AlbumArt { get; } = new List<AlbumArt>();
}
