using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.App.Services;

public sealed class TrackLibraryService : ITrackLibraryService
{
    private static readonly string[] DefaultSupportedExtensions =
    [
        ".mp3",
        ".flac",
        ".ogg",
        ".m4a",
        ".mp4",
        ".m4b",
        ".aac",
        ".alac"
    ];

    private readonly ITagReader _reader;
    private readonly ITagWriter _writer;
    private readonly HashSet<string> _supportedExtensions;

    public TrackLibraryService(ITagReader reader, ITagWriter writer, IEnumerable<string>? supportedExtensions = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));

        _supportedExtensions = new HashSet<string>(
            (supportedExtensions ?? DefaultSupportedExtensions).Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<TrackLoadResult> LoadTracksAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        return Task.Run(() =>
        {
            var tracks = new List<LoadedTrack>();
            var errors = new List<TrackOperationError>();

            if (!Directory.Exists(folderPath))
            {
                errors.Add(new TrackOperationError(folderPath, "Folder does not exist."));
                return new TrackLoadResult(tracks, errors);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
                    .Where(path => _supportedExtensions.Contains(NormalizeExtension(Path.GetExtension(path))))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                errors.Add(new TrackOperationError(folderPath, ex.Message));
                return new TrackLoadResult(tracks, errors);
            }

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var tags = _reader.Read(filePath);
                    tracks.Add(new LoadedTrack(filePath, tags));
                }
                catch (Exception ex)
                {
                    errors.Add(new TrackOperationError(filePath, ex.Message));
                }
            }

            return new TrackLoadResult(tracks, errors);
        }, cancellationToken);
    }

    public Task<TrackSaveResult> SaveTracksAsync(IReadOnlyList<TrackSaveRequest> requests, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        return Task.Run(() =>
        {
            var saved = new List<string>();
            var errors = new List<TrackOperationError>();

            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(request.FilePath))
                {
                    errors.Add(new TrackOperationError(string.Empty, "Cannot save track with an empty file path."));
                    continue;
                }

                if (request.Tags is null)
                {
                    errors.Add(new TrackOperationError(request.FilePath, "Cannot save track with missing tags."));
                    continue;
                }

                try
                {
                    _writer.Write(request.FilePath, request.Tags);
                    saved.Add(request.FilePath);
                }
                catch (Exception ex)
                {
                    errors.Add(new TrackOperationError(request.FilePath, ex.Message));
                }
            }

            return new TrackSaveResult(saved, errors);
        }, cancellationToken);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim();
        return normalized.StartsWith('.') ? normalized.ToLowerInvariant() : $".{normalized.ToLowerInvariant()}";
    }
}
