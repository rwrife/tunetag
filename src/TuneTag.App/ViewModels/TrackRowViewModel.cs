using System.Globalization;
using TuneTag.Core.Models;

namespace TuneTag.App.ViewModels;

public sealed class TrackRowViewModel : ViewModelBase
{
    private readonly string _filePath;
    private string? _title;
    private string? _artist;
    private string? _album;
    private string? _albumArtist;
    private uint? _trackNumber;
    private uint? _discNumber;
    private uint? _year;
    private string? _genre;
    private string? _composer;
    private string? _comment;
    private bool _isDirty;

    private TrackSnapshot _original;

    private TrackRowViewModel(string filePath, TrackTags tags)
    {
        _filePath = filePath;
        _title = tags.Title;
        _artist = tags.Artist;
        _album = tags.Album;
        _albumArtist = tags.AlbumArtist;
        _trackNumber = tags.TrackNumber;
        _discNumber = tags.DiscNumber;
        _year = tags.Year;
        _genre = tags.Genre;
        _composer = tags.Composer;
        _comment = tags.Comment;

        _original = CurrentSnapshot();
    }

    public string FilePath => _filePath;

    public string FileName => Path.GetFileName(_filePath);

    public string? Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public string? Artist
    {
        get => _artist;
        set
        {
            if (SetProperty(ref _artist, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public string? Album
    {
        get => _album;
        set
        {
            if (SetProperty(ref _album, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public string? AlbumArtist
    {
        get => _albumArtist;
        set
        {
            if (SetProperty(ref _albumArtist, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public uint? TrackNumber
    {
        get => _trackNumber;
        set
        {
            if (SetProperty(ref _trackNumber, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public uint? DiscNumber
    {
        get => _discNumber;
        set
        {
            if (SetProperty(ref _discNumber, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public uint? Year
    {
        get => _year;
        set
        {
            if (SetProperty(ref _year, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public string? Genre
    {
        get => _genre;
        set
        {
            if (SetProperty(ref _genre, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public string? Composer
    {
        get => _composer;
        set
        {
            if (SetProperty(ref _composer, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public string? Comment
    {
        get => _comment;
        set
        {
            if (SetProperty(ref _comment, value))
            {
                RecalculateDirtyState();
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public static TrackRowViewModel FromTrackTags(string filePath, TrackTags tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(tags);

        return new TrackRowViewModel(filePath, tags);
    }

    public void ApplyBatchValue(BatchEditField field, string? rawValue)
    {
        switch (field)
        {
            case BatchEditField.Title:
                Title = NormalizeText(rawValue);
                break;
            case BatchEditField.Artist:
                Artist = NormalizeText(rawValue);
                break;
            case BatchEditField.Album:
                Album = NormalizeText(rawValue);
                break;
            case BatchEditField.AlbumArtist:
                AlbumArtist = NormalizeText(rawValue);
                break;
            case BatchEditField.TrackNumber:
                TrackNumber = ParseOptionalUInt(rawValue, "Track number");
                break;
            case BatchEditField.DiscNumber:
                DiscNumber = ParseOptionalUInt(rawValue, "Disc number");
                break;
            case BatchEditField.Year:
                Year = ParseOptionalUInt(rawValue, "Year");
                break;
            case BatchEditField.Genre:
                Genre = NormalizeText(rawValue);
                break;
            case BatchEditField.Composer:
                Composer = NormalizeText(rawValue);
                break;
            case BatchEditField.Comment:
                Comment = NormalizeText(rawValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported batch edit field.");
        }
    }

    public TrackTags ToTrackTags()
    {
        return new TrackTags
        {
            Title = Title,
            Artist = Artist,
            Album = Album,
            AlbumArtist = AlbumArtist,
            TrackNumber = TrackNumber,
            DiscNumber = DiscNumber,
            Year = Year,
            Genre = Genre,
            Composer = Composer,
            Comment = Comment
        };
    }

    public void AcceptChanges()
    {
        _original = CurrentSnapshot();
        RecalculateDirtyState();
    }

    private void RecalculateDirtyState()
    {
        IsDirty = !CurrentSnapshot().Equals(_original);
    }

    private TrackSnapshot CurrentSnapshot()
    {
        return new TrackSnapshot(
            Title,
            Artist,
            Album,
            AlbumArtist,
            TrackNumber,
            DiscNumber,
            Year,
            Genre,
            Composer,
            Comment);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static uint? ParseOptionalUInt(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!uint.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{fieldName} must be an unsigned integer.");
        }

        return parsed;
    }

    private readonly record struct TrackSnapshot(
        string? Title,
        string? Artist,
        string? Album,
        string? AlbumArtist,
        uint? TrackNumber,
        uint? DiscNumber,
        uint? Year,
        string? Genre,
        string? Composer,
        string? Comment);
}
