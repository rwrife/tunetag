using System.Globalization;
using System.Text.RegularExpressions;
using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public sealed partial class FilenameParser : IFilenameParser
{
    public FilenameTagSuggestion Parse(string fileNameWithoutExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameWithoutExtension);

        var candidate = NormalizeSegment(fileNameWithoutExtension);

        var trackArtistTitle = TrackArtistTitleRegex().Match(candidate);
        if (trackArtistTitle.Success)
        {
            return BuildSuggestion(
                "track-artist-title",
                trackArtistTitle.Groups["track"].Value,
                trackArtistTitle.Groups["artist"].Value,
                trackArtistTitle.Groups["title"].Value);
        }

        var trackTitle = TrackTitleRegex().Match(candidate);
        if (trackTitle.Success)
        {
            return BuildSuggestion(
                "track-title",
                trackTitle.Groups["track"].Value,
                artist: null,
                trackTitle.Groups["title"].Value);
        }

        var artistTitle = ArtistTitleRegex().Match(candidate);
        if (artistTitle.Success)
        {
            return BuildSuggestion(
                "artist-title",
                track: null,
                artistTitle.Groups["artist"].Value,
                artistTitle.Groups["title"].Value);
        }

        return new FilenameTagSuggestion(new TrackTags(), [], "none");
    }

    private static FilenameTagSuggestion BuildSuggestion(string pattern, string? track, string? artist, string? title)
    {
        var suggested = new TrackTags();
        var fields = new List<string>();

        if (!string.IsNullOrWhiteSpace(track) &&
            uint.TryParse(track.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var trackNumber))
        {
            suggested.TrackNumber = trackNumber;
            fields.Add(nameof(TrackTags.TrackNumber));
        }

        if (!string.IsNullOrWhiteSpace(artist))
        {
            suggested.Artist = NormalizeSegment(artist);
            fields.Add(nameof(TrackTags.Artist));
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            suggested.Title = NormalizeSegment(title);
            fields.Add(nameof(TrackTags.Title));
        }

        return new FilenameTagSuggestion(suggested, fields, pattern);
    }

    private static string NormalizeSegment(string value)
    {
        return value
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Trim();
    }

    [GeneratedRegex("^(?<track>\\d{1,3})\\s*[-–]\\s*(?<artist>.+?)\\s*[-–]\\s*(?<title>.+)$", RegexOptions.Compiled)]
    private static partial Regex TrackArtistTitleRegex();

    [GeneratedRegex("^(?<track>\\d{1,3})\\s*[-–]\\s*(?<title>.+)$", RegexOptions.Compiled)]
    private static partial Regex TrackTitleRegex();

    [GeneratedRegex("^(?<artist>.+?)\\s*[-–]\\s*(?<title>.+)$", RegexOptions.Compiled)]
    private static partial Regex ArtistTitleRegex();
}
