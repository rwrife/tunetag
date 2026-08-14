using System.Text.Json.Serialization;

namespace TuneTag.Core.Models;

public sealed record RenameTrackInput(string FilePath, TrackTags Tags);

public sealed record RenamePlanEntry(
    string OriginalPath,
    string TargetPath,
    bool CollisionResolved,
    string? CollisionNote)
{
    public bool WillRename => !string.Equals(OriginalPath, TargetPath, StringComparison.OrdinalIgnoreCase);
}

public sealed class RenamePreviewResult
{
    public RenamePreviewResult(IReadOnlyList<RenamePlanEntry> entries)
    {
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public IReadOnlyList<RenamePlanEntry> Entries { get; }

    public int RenameCount => Entries.Count(static entry => entry.WillRename);

    public int CollisionCount => Entries.Count(static entry => entry.CollisionResolved);
}

public sealed record RenameApplyResult(
    int RenamedCount,
    string UndoJournalPath,
    IReadOnlyList<RenamePlanEntry> AppliedEntries);

public sealed record RenameUndoResult(
    int RestoredCount,
    string UndoJournalPath,
    IReadOnlyList<RenamePlanEntry> RestoredEntries);

public sealed class RenameUndoJournal
{
    public string Version { get; init; } = "1";

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public List<RenameUndoJournalEntry> Entries { get; init; } = [];

    [JsonIgnore]
    public bool IsEmpty => Entries.Count == 0;
}

public sealed class RenameUndoJournalEntry
{
    public string OriginalPath { get; init; } = string.Empty;

    public string RenamedPath { get; init; } = string.Empty;
}
