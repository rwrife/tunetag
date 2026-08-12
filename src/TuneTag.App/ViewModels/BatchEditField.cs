namespace TuneTag.App.ViewModels;

public enum BatchEditField
{
    Title,
    Artist,
    Album,
    AlbumArtist,
    TrackNumber,
    DiscNumber,
    Year,
    Genre,
    Composer,
    Comment
}

public sealed record BatchEditFieldOption(BatchEditField Field, string DisplayName);
