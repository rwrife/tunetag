using TuneTag.Core.Models;

namespace TuneTag.App.Services;

public interface ITrackLibraryService
{
    Task<TrackLoadResult> LoadTracksAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<TrackSaveResult> SaveTracksAsync(IReadOnlyList<TrackSaveRequest> requests, CancellationToken cancellationToken = default);
}

public sealed record LoadedTrack(string FilePath, TrackTags Tags);

public sealed record TrackOperationError(string FilePath, string Message);

public sealed record TrackLoadResult(IReadOnlyList<LoadedTrack> Tracks, IReadOnlyList<TrackOperationError> Errors)
{
    public int TrackCount => Tracks.Count;
}

public sealed record TrackSaveRequest(string FilePath, TrackTags Tags);

public sealed record TrackSaveResult(IReadOnlyList<string> SavedFilePaths, IReadOnlyList<TrackOperationError> Errors)
{
    public int SavedCount => SavedFilePaths.Count;
}
