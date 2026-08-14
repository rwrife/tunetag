using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public interface IRenameEngine
{
    RenamePreviewResult BuildPreview(IReadOnlyList<RenameTrackInput> tracks, string template);

    RenameApplyResult Apply(RenamePreviewResult preview, string? undoJournalPath = null);

    RenameUndoResult Undo(string undoJournalPath);
}
