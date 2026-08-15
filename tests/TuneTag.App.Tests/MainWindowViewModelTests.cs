using TuneTag.App.Services;
using TuneTag.App.ViewModels;
using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task LoadFolderAsync_PopulatesTracksAndStatus()
    {
        var service = new FakeTrackLibraryService
        {
            NextLoadResult = new TrackLoadResult(
            [
                new LoadedTrack("/music/one.mp3", new TrackTags { Title = "One" }),
                new LoadedTrack("/music/two.flac", new TrackTags { Title = "Two" })
            ],
            [
                new TrackOperationError("/music/broken.mp3", "Unsupported format")
            ])
        };

        var vm = new MainWindowViewModel(service, new FakeArtService());

        await vm.LoadFolderAsync("/music");

        Assert.Equal(2, vm.Tracks.Count);
        Assert.Contains("Loaded 2 tracks", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 file(s) failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, vm.DirtyTrackCount);
        Assert.False(vm.HasDirtyTracks);
    }

    [Fact]
    public async Task ApplyBatchEditToSelection_UpdatesAllSelectedTracks()
    {
        var service = new FakeTrackLibraryService
        {
            NextLoadResult = new TrackLoadResult(
            [
                new LoadedTrack("/music/one.mp3", new TrackTags { Album = "Old" }),
                new LoadedTrack("/music/two.flac", new TrackTags { Album = "Old" })
            ],
            [])
        };

        var vm = new MainWindowViewModel(service, new FakeArtService());
        await vm.LoadFolderAsync("/music");

        vm.SetSelectedTracks(vm.Tracks);
        vm.SelectedBatchEditField = vm.BatchEditFields.Single(option => option.Field == BatchEditField.Album);
        vm.BatchEditValue = "New Album";

        var applied = vm.ApplyBatchEditToSelection();

        Assert.True(applied);
        Assert.All(vm.Tracks, track => Assert.Equal("New Album", track.Album));
        Assert.Equal(2, vm.DirtyTrackCount);
        Assert.True(vm.HasDirtyTracks);
    }

    [Fact]
    public async Task SaveAsync_PersistsDirtyTracksAndClearsDirtyState()
    {
        var service = new FakeTrackLibraryService
        {
            NextLoadResult = new TrackLoadResult(
            [
                new LoadedTrack("/music/one.mp3", new TrackTags { Album = "Old" })
            ],
            [])
        };

        service.SaveHandler = requests => new TrackSaveResult(requests.Select(request => request.FilePath).ToArray(), []);

        var vm = new MainWindowViewModel(service, new FakeArtService());
        await vm.LoadFolderAsync("/music");

        vm.SetSelectedTracks(vm.Tracks);
        vm.SelectedBatchEditField = vm.BatchEditFields.Single(option => option.Field == BatchEditField.Album);
        vm.BatchEditValue = "New Album";
        vm.ApplyBatchEditToSelection();

        await vm.SaveAsync();

        var savedRequest = Assert.Single(service.CapturedSaveRequests);
        Assert.Equal("/music/one.mp3", savedRequest.FilePath);
        Assert.Equal("New Album", savedRequest.Tags.Album);

        Assert.False(vm.Tracks[0].IsDirty);
        Assert.False(vm.HasDirtyTracks);
        Assert.Contains("Saved 1 track", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshRenamePreview_ShowsLiveOldToNewMappings()
    {
        var service = new FakeTrackLibraryService
        {
            NextLoadResult = new TrackLoadResult(
            [
                new LoadedTrack("/music/one.mp3", new TrackTags { TrackNumber = 1, Title = "Intro" }),
                new LoadedTrack("/music/two.mp3", new TrackTags { TrackNumber = 2, Title = "Outro" })
            ],
            [])
        };

        var vm = new MainWindowViewModel(service, new FakeArtService());
        await vm.LoadFolderAsync("/music");

        vm.RenameTemplate = "{track:00} - {title}";
        var built = vm.RefreshRenamePreview();

        Assert.True(built);
        Assert.Contains("one.mp3 -> 01 - Intro.mp3", vm.RenamePreviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("two.mp3 -> 02 - Outro.mp3", vm.RenamePreviewText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestTagsFromFilenames_ParsesCommonPattern_WithoutSaving()
    {
        var service = new FakeTrackLibraryService
        {
            NextLoadResult = new TrackLoadResult(
            [
                new LoadedTrack("/music/01 - Artist Name - Song Title.mp3", new TrackTags())
            ],
            [])
        };

        var vm = new MainWindowViewModel(service, new FakeArtService());
        await vm.LoadFolderAsync("/music");

        vm.SetSelectedTracks(vm.Tracks);
        var updated = vm.SuggestTagsFromFilenames();

        Assert.Equal(1, updated);
        Assert.Equal((uint)1, vm.Tracks[0].TrackNumber);
        Assert.Equal("Artist Name", vm.Tracks[0].Artist);
        Assert.Equal("Song Title", vm.Tracks[0].Title);
        Assert.Empty(service.CapturedSaveRequests);
    }

    [Fact]
    public async Task ProbeAiServiceAsync_WhenLocalModelUnavailable_DisablesSuggestionsWithClearMessage()
    {
        var service = new FakeTrackLibraryService();
        var fakeAi = new FakeTagAiService
        {
            ProbeResult = new TagAiProbeResult(false, "Could not reach local model endpoint: connection refused")
        };

        var vm = new MainWindowViewModel(service, new FakeArtService())
        {
            TagAiService = fakeAi,
            AiAssistEnabled = true
        };

        var reachable = await vm.ProbeAiServiceAsync();

        Assert.False(reachable);
        Assert.False(vm.CanRequestAiSuggestions);
        Assert.Contains("could not reach local model endpoint", vm.AiAssistStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAiSuggestions_DoesNotWriteUntilApplyIsExplicitlyCalled()
    {
        var service = new FakeTrackLibraryService
        {
            NextLoadResult = new TrackLoadResult(
            [
                new LoadedTrack("/music/one.mp3", new TrackTags { Artist = "Daft Punk" })
            ],
            [])
        };

        var fakeAi = new FakeTagAiService
        {
            ProbeResult = new TagAiProbeResult(true, "reachable"),
            Suggestions =
            [
                new TagAiSuggestion(
                    "/music/one.mp3",
                    new TrackTags { Title = "One More Time", Genre = "French House" },
                    "well-known track")
            ]
        };

        var vm = new MainWindowViewModel(service, new FakeArtService())
        {
            TagAiService = fakeAi,
            AiAssistEnabled = true
        };

        await vm.LoadFolderAsync("/music");
        vm.SetSelectedTracks(vm.Tracks);

        var suggested = await vm.GenerateAiSuggestionsAsync();

        Assert.Equal(1, suggested);
        Assert.True(vm.HasPendingAiSuggestions);
        Assert.Null(vm.Tracks[0].Title);
        Assert.Null(vm.Tracks[0].Genre);
        Assert.False(vm.Tracks[0].IsDirty);

        var applied = vm.ApplyAiSuggestions();

        Assert.Equal(1, applied);
        Assert.Equal("One More Time", vm.Tracks[0].Title);
        Assert.Equal("French House", vm.Tracks[0].Genre);
        Assert.True(vm.Tracks[0].IsDirty);
    }

    private sealed class FakeTrackLibraryService : ITrackLibraryService
    {
        public TrackLoadResult NextLoadResult { get; set; } = new([], []);

        public Func<IReadOnlyList<TrackSaveRequest>, TrackSaveResult>? SaveHandler { get; set; }

        public List<TrackSaveRequest> CapturedSaveRequests { get; } = [];

        public Task<TrackLoadResult> LoadTracksAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextLoadResult);
        }

        public Task<TrackSaveResult> SaveTracksAsync(IReadOnlyList<TrackSaveRequest> requests, CancellationToken cancellationToken = default)
        {
            CapturedSaveRequests.AddRange(requests);

            if (SaveHandler is not null)
            {
                return Task.FromResult(SaveHandler(requests));
            }

            return Task.FromResult(new TrackSaveResult([], []));
        }
    }

    private sealed class FakeArtService : IArtService
    {
        public AlbumArt? ReadPrimary(string filePath) => null;

        public void SetPrimary(string filePath, AlbumArt art)
        {
        }

        public void Remove(string filePath)
        {
        }

        public string ExtractPrimary(string filePath, string outputPath)
        {
            return outputPath;
        }

        public int ApplyPrimaryToFolder(string folderPath, AlbumArt art, IEnumerable<string>? supportedExtensions = null)
        {
            return 0;
        }
    }

    private sealed class FakeTagAiService : ITagAiService
    {
        public TagAiProbeResult ProbeResult { get; set; } = new(true, "reachable");

        public IReadOnlyList<TagAiSuggestion> Suggestions { get; set; } = [];

        public Task<TagAiProbeResult> ProbeAsync(TagAiRequestOptions options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProbeResult);
        }

        public Task<IReadOnlyList<TagAiSuggestion>> SuggestMissingTagsAsync(
            IReadOnlyList<TagAiTrackInput> tracks,
            TagAiRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Suggestions);
        }
    }
}
