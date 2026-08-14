using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.Core.Tests;

public sealed class RenameEngineTests
{
    [Fact]
    public void BuildPreview_TemplateTrackAndTitle_GeneratesExpectedDryRunMapping()
    {
        var engine = new RenameEngine();
        var folder = CreateTempFolder();

        var first = CreateFile(folder, "a.mp3");
        var second = CreateFile(folder, "b.mp3");

        var preview = engine.BuildPreview(
        [
            new RenameTrackInput(first, new TrackTags { TrackNumber = 1, Title = "First Song" }),
            new RenameTrackInput(second, new TrackTags { TrackNumber = 2, Title = "Second Song" })
        ],
        "{track:00} - {title}");

        Assert.Equal(2, preview.RenameCount);
        Assert.Contains(preview.Entries, entry => entry.TargetPath.EndsWith("01 - First Song.mp3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Entries, entry => entry.TargetPath.EndsWith("02 - Second Song.mp3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildPreview_SanitizesInvalidCharacters_AndResolvesNameCollisions()
    {
        var engine = new RenameEngine();
        var folder = CreateTempFolder();

        var first = CreateFile(folder, "x.mp3");
        var second = CreateFile(folder, "y.mp3");

        var preview = engine.BuildPreview(
        [
            new RenameTrackInput(first, new TrackTags { Title = "A/B" }),
            new RenameTrackInput(second, new TrackTags { Title = "A:B" })
        ],
        "{title}");

        Assert.Equal(2, preview.RenameCount);
        Assert.Equal(1, preview.CollisionCount);
        Assert.Contains(preview.Entries, entry => entry.TargetPath.EndsWith("A_B.mp3", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.Entries, entry => entry.TargetPath.EndsWith("A_B (2).mp3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplyThenUndo_RestoresOriginalFileNamesFromJournal()
    {
        var engine = new RenameEngine();
        var folder = CreateTempFolder();

        var first = CreateFile(folder, "track-a.mp3");
        var second = CreateFile(folder, "track-b.mp3");

        var preview = engine.BuildPreview(
        [
            new RenameTrackInput(first, new TrackTags { TrackNumber = 1, Title = "Alpha" }),
            new RenameTrackInput(second, new TrackTags { TrackNumber = 2, Title = "Beta" })
        ],
        "{track:00} - {title}");

        var applyResult = engine.Apply(preview);

        Assert.True(File.Exists(applyResult.UndoJournalPath));
        Assert.All(applyResult.AppliedEntries, entry => Assert.True(File.Exists(entry.TargetPath)));
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));

        var undoResult = engine.Undo(applyResult.UndoJournalPath);

        Assert.Equal(2, undoResult.RestoredCount);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.DoesNotContain(undoResult.RestoredEntries, entry => File.Exists(entry.OriginalPath));
    }

    [Theory]
    [InlineData("01 - Artist Name - Track Title", 1u, "Artist Name", "Track Title")]
    [InlineData("Artist Name - Track Title", null, "Artist Name", "Track Title")]
    [InlineData("07 - Track Title", 7u, null, "Track Title")]
    public void FilenameParser_ParsesCommonPatterns(string fileName, uint? expectedTrack, string? expectedArtist, string? expectedTitle)
    {
        var parser = new FilenameParser();

        var suggestion = parser.Parse(fileName);

        Assert.Equal(expectedTrack, suggestion.SuggestedTags.TrackNumber);
        Assert.Equal(expectedArtist, suggestion.SuggestedTags.Artist);
        Assert.Equal(expectedTitle, suggestion.SuggestedTags.Title);
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"tunetag-rename-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string CreateFile(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "fixture");
        return path;
    }
}
