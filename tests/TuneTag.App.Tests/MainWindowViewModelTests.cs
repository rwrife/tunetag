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
}
