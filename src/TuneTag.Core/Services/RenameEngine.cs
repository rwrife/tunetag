using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public sealed class RenameEngine : IRenameEngine
{
    private static readonly Regex TokenRegex = new("\\{(?<token>[a-zA-Z]+)(:(?<format>[^}]+))?\\}", RegexOptions.Compiled);

    public RenamePreviewResult BuildPreview(IReadOnlyList<RenameTrackInput> tracks, string template)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var intents = tracks
            .Select(track => BuildIntent(track, template))
            .ToArray();

        var entries = new List<RenamePlanEntry>(intents.Length);

        foreach (var directoryGroup in intents.GroupBy(static intent => intent.DirectoryPath, StringComparer.OrdinalIgnoreCase))
        {
            var directory = directoryGroup.Key;
            var groupItems = directoryGroup
                .OrderBy(static item => item.OriginalPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var originalPaths = groupItems
                .Select(static item => item.OriginalPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingPaths = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var reservedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in groupItems)
            {
                var candidate = item.DesiredPath;
                var collisionResolved = false;
                string? collisionNote = null;

                if (IsCollision(candidate, item.OriginalPath, originalPaths, existingPaths, reservedTargets))
                {
                    collisionResolved = true;
                    var nextOrdinal = 2;
                    while (IsCollision(candidate, item.OriginalPath, originalPaths, existingPaths, reservedTargets))
                    {
                        candidate = AddNumericSuffix(item.DesiredPath, nextOrdinal);
                        nextOrdinal++;
                    }

                    collisionNote = "Name collision resolved with numeric suffix.";
                }

                reservedTargets.Add(candidate);
                entries.Add(new RenamePlanEntry(item.OriginalPath, candidate, collisionResolved, collisionNote));
            }
        }

        return new RenamePreviewResult(entries);
    }

    public RenameApplyResult Apply(RenamePreviewResult preview, string? undoJournalPath = null)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var toRename = preview.Entries
            .Where(static entry => entry.WillRename)
            .ToArray();

        if (toRename.Length == 0)
        {
            var emptyJournalPath = GetJournalPath(preview.Entries, undoJournalPath);
            WriteJournal(emptyJournalPath, []);
            return new RenameApplyResult(0, emptyJournalPath, []);
        }

        var phaseMoves = new List<PhaseMove>(toRename.Length);

        foreach (var entry in toRename)
        {
            if (!File.Exists(entry.OriginalPath))
            {
                throw new FileNotFoundException($"Cannot rename missing file: {entry.OriginalPath}");
            }

            var tempPath = BuildTemporaryPath(entry.OriginalPath);
            File.Move(entry.OriginalPath, tempPath);
            phaseMoves.Add(new PhaseMove(entry.OriginalPath, tempPath, entry.TargetPath));
        }

        try
        {
            foreach (var move in phaseMoves)
            {
                var directory = Path.GetDirectoryName(move.TargetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(move.TempPath, move.TargetPath);
            }
        }
        catch
        {
            RollbackPhaseMoves(phaseMoves);
            throw;
        }

        var journalPath = GetJournalPath(preview.Entries, undoJournalPath);
        WriteJournal(journalPath, toRename);

        return new RenameApplyResult(toRename.Length, journalPath, toRename);
    }

    public RenameUndoResult Undo(string undoJournalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(undoJournalPath);

        if (!File.Exists(undoJournalPath))
        {
            throw new FileNotFoundException($"Undo journal not found: {undoJournalPath}");
        }

        var journalJson = File.ReadAllText(undoJournalPath);
        var journal = JsonSerializer.Deserialize<RenameUndoJournal>(journalJson, JsonOptions)
            ?? throw new InvalidOperationException("Undo journal could not be parsed.");

        var entries = journal.Entries
            .Where(static entry =>
                !string.IsNullOrWhiteSpace(entry.OriginalPath) &&
                !string.IsNullOrWhiteSpace(entry.RenamedPath) &&
                !string.Equals(entry.OriginalPath, entry.RenamedPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (entries.Length == 0)
        {
            return new RenameUndoResult(0, undoJournalPath, []);
        }

        var phaseMoves = new List<PhaseMove>(entries.Length);

        foreach (var entry in entries)
        {
            if (!File.Exists(entry.RenamedPath))
            {
                throw new FileNotFoundException($"Cannot undo missing renamed file: {entry.RenamedPath}");
            }

            var tempPath = BuildTemporaryPath(entry.RenamedPath);
            File.Move(entry.RenamedPath, tempPath);
            phaseMoves.Add(new PhaseMove(entry.RenamedPath, tempPath, entry.OriginalPath));
        }

        try
        {
            foreach (var move in phaseMoves)
            {
                var directory = Path.GetDirectoryName(move.TargetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(move.TempPath, move.TargetPath);
            }
        }
        catch
        {
            RollbackPhaseMoves(phaseMoves);
            throw;
        }

        var restored = entries
            .Select(static entry => new RenamePlanEntry(entry.RenamedPath, entry.OriginalPath, false, null))
            .ToArray();

        return new RenameUndoResult(restored.Length, undoJournalPath, restored);
    }

    private static RenameIntent BuildIntent(RenameTrackInput track, string template)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentException.ThrowIfNullOrWhiteSpace(track.FilePath);
        ArgumentNullException.ThrowIfNull(track.Tags);

        var originalPath = Path.GetFullPath(track.FilePath);
        var directory = Path.GetDirectoryName(originalPath)
            ?? throw new InvalidOperationException($"Unable to resolve folder for path: {track.FilePath}");

        var extension = Path.GetExtension(originalPath);
        var rendered = RenderTemplate(template, track.Tags);
        var sanitized = SanitizeFileName(rendered);

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "untitled";
        }

        var desiredPath = Path.Combine(directory, sanitized + extension);
        return new RenameIntent(originalPath, directory, desiredPath);
    }

    private static string RenderTemplate(string template, TrackTags tags)
    {
        var rendered = TokenRegex.Replace(template, match =>
        {
            var token = match.Groups["token"].Value;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
            return ResolveToken(token, format, tags);
        });

        return Regex.Replace(rendered, "\\s+", " ").Trim();
    }

    private static string ResolveToken(string token, string? format, TrackTags tags)
    {
        return token.ToLowerInvariant() switch
        {
            "artist" => tags.Artist ?? string.Empty,
            "album" => tags.Album ?? string.Empty,
            "albumartist" => tags.AlbumArtist ?? string.Empty,
            "title" => tags.Title ?? string.Empty,
            "track" => FormatOptionalUInt(tags.TrackNumber, format),
            "disc" => FormatOptionalUInt(tags.DiscNumber, format),
            "year" => FormatOptionalUInt(tags.Year, format),
            "genre" => tags.Genre ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string FormatOptionalUInt(uint? value, string? format)
    {
        if (!value.HasValue)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(format)
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : value.Value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string SanitizeFileName(string value)
    {
        var osInvalidChars = Path.GetInvalidFileNameChars();
        var windowsInvalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        var sanitizedChars = value
            .Select(ch =>
                osInvalidChars.Contains(ch) ||
                windowsInvalidChars.Contains(ch) ||
                char.IsControl(ch)
                    ? '_'
                    : ch)
            .ToArray();

        var sanitized = new string(sanitizedChars)
            .Trim()
            .TrimEnd('.', ' ');

        return Regex.Replace(sanitized, "\\s+", " ").Trim();
    }

    private static bool IsCollision(
        string candidatePath,
        string originalPath,
        ISet<string> originalPaths,
        ISet<string> existingPaths,
        ISet<string> reservedTargets)
    {
        if (reservedTargets.Contains(candidatePath))
        {
            return true;
        }

        if (string.Equals(candidatePath, originalPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return existingPaths.Contains(candidatePath) && !originalPaths.Contains(candidatePath);
    }

    private static string AddNumericSuffix(string path, int ordinal)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var extension = Path.GetExtension(path);
        var baseName = Path.GetFileNameWithoutExtension(path);
        var fileName = $"{baseName} ({ordinal}){extension}";
        return Path.Combine(directory, fileName);
    }

    private static string BuildTemporaryPath(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException($"Unable to resolve folder for path: {sourcePath}");
        var extension = Path.GetExtension(sourcePath);

        string tempPath;
        do
        {
            tempPath = Path.Combine(directory, $".tunetag-tmp-{Guid.NewGuid():N}{extension}");
        }
        while (File.Exists(tempPath));

        return tempPath;
    }

    private static void RollbackPhaseMoves(IEnumerable<PhaseMove> phaseMoves)
    {
        foreach (var move in phaseMoves.Reverse())
        {
            try
            {
                if (File.Exists(move.TempPath) && !File.Exists(move.OriginalPath))
                {
                    File.Move(move.TempPath, move.OriginalPath);
                    continue;
                }

                if (File.Exists(move.TargetPath) && !File.Exists(move.OriginalPath))
                {
                    File.Move(move.TargetPath, move.OriginalPath);
                }
            }
            catch
            {
                // Best effort rollback.
            }
        }
    }

    private static string GetJournalPath(IReadOnlyList<RenamePlanEntry> entries, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var firstPath = entries.FirstOrDefault()?.OriginalPath;
        var baseDirectory = !string.IsNullOrWhiteSpace(firstPath)
            ? Path.GetDirectoryName(firstPath)
            : Directory.GetCurrentDirectory();

        var fileName = $".tunetag-undo-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
        return Path.Combine(baseDirectory ?? Directory.GetCurrentDirectory(), fileName);
    }

    private static void WriteJournal(string journalPath, IReadOnlyList<RenamePlanEntry> appliedEntries)
    {
        var directory = Path.GetDirectoryName(journalPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var journal = new RenameUndoJournal
        {
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Entries = appliedEntries
                .Select(static entry => new RenameUndoJournalEntry
                {
                    OriginalPath = entry.OriginalPath,
                    RenamedPath = entry.TargetPath
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(journal, JsonOptions);
        File.WriteAllText(journalPath, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record RenameIntent(string OriginalPath, string DirectoryPath, string DesiredPath);

    private sealed record PhaseMove(string OriginalPath, string TempPath, string TargetPath);
}
