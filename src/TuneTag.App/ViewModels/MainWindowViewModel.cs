using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Media.Imaging;
using TuneTag.App.Services;
using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITrackLibraryService _trackLibraryService;
    private readonly IArtService _artService;
    private readonly IRenameEngine _renameEngine;
    private readonly IFilenameParser _filenameParser;
    private readonly HashSet<TrackRowViewModel> _selectedTracks = [];

    private BatchEditFieldOption? _selectedBatchEditField;
    private string? _batchEditValue;
    private string _statusMessage = "Open a folder to begin editing tags.";
    private bool _isBusy;
    private int _selectedTrackCount;
    private AlbumArt? _currentCover;
    private Bitmap? _currentCoverPreview;
    private string _currentCoverSummary = "Select a track to view album art.";
    private string _renameTemplate = "{track:00} - {title}";
    private string _renamePreviewText = "Rename preview appears here.";
    private string _renameSummary = "No rename preview yet.";
    private IReadOnlyList<RenamePlanEntry> _renamePlan = [];
    private string? _lastRenameJournalPath;
    private string? _currentFolderPath;

    public MainWindowViewModel(
        ITrackLibraryService trackLibraryService,
        IArtService artService,
        IRenameEngine? renameEngine = null,
        IFilenameParser? filenameParser = null)
    {
        _trackLibraryService = trackLibraryService ?? throw new ArgumentNullException(nameof(trackLibraryService));
        _artService = artService ?? throw new ArgumentNullException(nameof(artService));
        _renameEngine = renameEngine ?? new RenameEngine();
        _filenameParser = filenameParser ?? new FilenameParser();

        Tracks = [];
        BatchEditFields =
        [
            new BatchEditFieldOption(BatchEditField.Title, "Title"),
            new BatchEditFieldOption(BatchEditField.Artist, "Artist"),
            new BatchEditFieldOption(BatchEditField.Album, "Album"),
            new BatchEditFieldOption(BatchEditField.AlbumArtist, "Album Artist"),
            new BatchEditFieldOption(BatchEditField.TrackNumber, "Track #"),
            new BatchEditFieldOption(BatchEditField.DiscNumber, "Disc #"),
            new BatchEditFieldOption(BatchEditField.Year, "Year"),
            new BatchEditFieldOption(BatchEditField.Genre, "Genre"),
            new BatchEditFieldOption(BatchEditField.Composer, "Composer"),
            new BatchEditFieldOption(BatchEditField.Comment, "Comment")
        ];

        SelectedBatchEditField = BatchEditFields[0];
    }

    public ObservableCollection<TrackRowViewModel> Tracks { get; }

    public IReadOnlyList<BatchEditFieldOption> BatchEditFields { get; }

    public BatchEditFieldOption? SelectedBatchEditField
    {
        get => _selectedBatchEditField;
        set => SetProperty(ref _selectedBatchEditField, value);
    }

    public string? BatchEditValue
    {
        get => _batchEditValue;
        set => SetProperty(ref _batchEditValue, value);
    }

    public string RenameTemplate
    {
        get => _renameTemplate;
        set
        {
            if (SetProperty(ref _renameTemplate, value))
            {
                RefreshRenamePreview();
            }
        }
    }

    public string RenamePreviewText
    {
        get => _renamePreviewText;
        private set => SetProperty(ref _renamePreviewText, value);
    }

    public string RenameSummary
    {
        get => _renameSummary;
        private set => SetProperty(ref _renameSummary, value);
    }

    public bool HasRenamePlan => _renamePlan.Count > 0;

    public string? LastRenameJournalPath
    {
        get => _lastRenameJournalPath;
        private set => SetProperty(ref _lastRenameJournalPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public int SelectedTrackCount
    {
        get => _selectedTrackCount;
        private set
        {
            if (SetProperty(ref _selectedTrackCount, value))
            {
                RaisePropertyChanged(nameof(SelectionSummary));
                RaisePropertyChanged(nameof(HasSelectedTracks));
            }
        }
    }

    public int DirtyTrackCount => Tracks.Count(static track => track.IsDirty);

    public bool HasDirtyTracks => DirtyTrackCount > 0;

    public bool HasSelectedTracks => SelectedTrackCount > 0;

    public Bitmap? CurrentCoverPreview
    {
        get => _currentCoverPreview;
        private set => SetProperty(ref _currentCoverPreview, value);
    }

    public bool HasCurrentCover => _currentCover is not null;

    public string CurrentCoverSummary
    {
        get => _currentCoverSummary;
        private set => SetProperty(ref _currentCoverSummary, value);
    }

    public string SelectionSummary => $"{Tracks.Count} loaded • {SelectedTrackCount} selected • {DirtyTrackCount} dirty";

    public async Task LoadFolderAsync(string folderPath)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Loading tracks from {folderPath}...";

        try
        {
            var result = await _trackLibraryService.LoadTracksAsync(folderPath).ConfigureAwait(true);

            _currentFolderPath = folderPath;
            ReplaceTracks(result.Tracks.Select(track => TrackRowViewModel.FromTrackTags(track.FilePath, track.Tags)));
            SetSelectedTracks([]);
            BatchEditValue = string.Empty;

            if (result.Errors.Count == 0)
            {
                StatusMessage = $"Loaded {result.TrackCount} tracks from {folderPath}.";
            }
            else
            {
                StatusMessage = $"Loaded {result.TrackCount} tracks from {folderPath}; {result.Errors.Count} file(s) failed to load.";
            }

            RefreshTrackStateIndicators();
            RefreshRenamePreview();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetSelectedTracks(IEnumerable<TrackRowViewModel> selectedTracks)
    {
        ArgumentNullException.ThrowIfNull(selectedTracks);

        _selectedTracks.Clear();
        foreach (var track in selectedTracks)
        {
            _selectedTracks.Add(track);
        }

        SelectedTrackCount = _selectedTracks.Count;

        if (_selectedTracks.Count == 0)
        {
            SetCurrentCover(null, "Select a track to view album art.");
        }
        else
        {
            CurrentCoverSummary = "Loading cover from selected track...";
        }

        RefreshRenamePreview();
    }

    public bool ApplyBatchEditToSelection()
    {
        if (SelectedBatchEditField is null)
        {
            StatusMessage = "Choose a field before applying a batch edit.";
            return false;
        }

        if (_selectedTracks.Count == 0)
        {
            StatusMessage = "Select one or more tracks to apply batch edits.";
            return false;
        }

        try
        {
            foreach (var track in _selectedTracks)
            {
                track.ApplyBatchValue(SelectedBatchEditField.Field, BatchEditValue);
            }
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }

        StatusMessage = $"Applied {SelectedBatchEditField.DisplayName} to {_selectedTracks.Count} selected track(s).";
        RefreshTrackStateIndicators();
        return true;
    }

    public bool RefreshRenamePreview()
    {
        var tracks = GetTracksForRenameScope();
        if (tracks.Length == 0)
        {
            SetRenamePlan([]);
            RenameSummary = "No tracks available for rename preview.";
            RenamePreviewText = "Load a folder first.";
            return false;
        }

        try
        {
            var preview = _renameEngine.BuildPreview(
                tracks.Select(track => new RenameTrackInput(track.FilePath, track.ToTrackTags())).ToArray(),
                RenameTemplate);

            SetRenamePlan(preview.Entries);
            RenameSummary = $"Preview: {preview.RenameCount} rename(s), {preview.CollisionCount} collision(s) resolved.";

            var lines = preview.Entries
                .Select(entry =>
                {
                    var left = Path.GetFileName(entry.OriginalPath);
                    var right = Path.GetFileName(entry.TargetPath);
                    var note = entry.CollisionResolved && !string.IsNullOrWhiteSpace(entry.CollisionNote)
                        ? $" [{entry.CollisionNote}]"
                        : string.Empty;

                    return $"{left} -> {right}{note}";
                })
                .ToArray();

            RenamePreviewText = lines.Length == 0
                ? "No tracks available for preview."
                : string.Join(Environment.NewLine, lines);

            return true;
        }
        catch (Exception ex)
        {
            SetRenamePlan([]);
            RenameSummary = "Rename preview failed.";
            RenamePreviewText = ex.Message;
            return false;
        }
    }

    public async Task<bool> ApplyRenameAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (_renamePlan.Count == 0 && !RefreshRenamePreview())
        {
            StatusMessage = "Unable to build rename preview.";
            return false;
        }

        var preview = new RenamePreviewResult(_renamePlan);
        if (preview.RenameCount == 0)
        {
            StatusMessage = "No file name changes to apply from current template.";
            return false;
        }

        IsBusy = true;
        StatusMessage = $"Applying {preview.RenameCount} rename(s)...";

        try
        {
            var applyResult = await Task.Run(() => _renameEngine.Apply(preview)).ConfigureAwait(true);

            UpdateTrackPaths(applyResult.AppliedEntries);
            LastRenameJournalPath = applyResult.UndoJournalPath;
            StatusMessage = $"Renamed {applyResult.RenamedCount} file(s). Undo journal: {applyResult.UndoJournalPath}";

            RefreshRenamePreview();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rename apply failed: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> UndoLastRenameAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(LastRenameJournalPath))
        {
            StatusMessage = "No undo journal is available yet.";
            return false;
        }

        IsBusy = true;
        StatusMessage = "Undoing last rename operation...";

        try
        {
            var undoResult = await Task.Run(() => _renameEngine.Undo(LastRenameJournalPath)).ConfigureAwait(true);

            UpdateTrackPaths(undoResult.RestoredEntries);
            StatusMessage = $"Undo restored {undoResult.RestoredCount} file(s).";
            RefreshRenamePreview();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Undo failed: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public int SuggestTagsFromFilenames()
    {
        var tracks = GetTracksForRenameScope();
        if (tracks.Length == 0)
        {
            StatusMessage = "No tracks available for filename-based suggestions.";
            return 0;
        }

        var updatedTracks = 0;
        foreach (var track in tracks)
        {
            var fileName = Path.GetFileNameWithoutExtension(track.FileName);
            var suggestion = _filenameParser.Parse(fileName);
            if (!suggestion.HasSuggestions)
            {
                continue;
            }

            if (ApplyFilenameSuggestion(track, suggestion))
            {
                updatedTracks++;
            }
        }

        if (updatedTracks == 0)
        {
            StatusMessage = "No filename patterns matched selected tracks.";
        }
        else
        {
            StatusMessage = $"Applied filename tag suggestions to {updatedTracks} track(s). Review before saving.";
        }

        RefreshTrackStateIndicators();
        RefreshRenamePreview();
        return updatedTracks;
    }

    public async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var dirtyTracks = Tracks.Where(static track => track.IsDirty).ToArray();
        if (dirtyTracks.Length == 0)
        {
            StatusMessage = "No pending changes to save.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Saving {dirtyTracks.Length} modified track(s)...";

        try
        {
            var requests = dirtyTracks
                .Select(track => new TrackSaveRequest(track.FilePath, track.ToTrackTags()))
                .ToArray();

            var result = await _trackLibraryService.SaveTracksAsync(requests).ConfigureAwait(true);
            var savedSet = result.SavedFilePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var track in dirtyTracks)
            {
                if (savedSet.Contains(track.FilePath))
                {
                    track.AcceptChanges();
                }
            }

            if (result.Errors.Count == 0)
            {
                StatusMessage = $"Saved {result.SavedCount} track(s).";
            }
            else
            {
                StatusMessage = $"Saved {result.SavedCount} track(s); {result.Errors.Count} track(s) failed to save.";
            }

            RefreshTrackStateIndicators();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshSelectedCoverAsync()
    {
        var selectedTrack = GetPrimarySelectedTrack();
        if (selectedTrack is null)
        {
            SetCurrentCover(null, "Select a track to view album art.");
            return;
        }

        try
        {
            var art = await Task.Run(() => _artService.ReadPrimary(selectedTrack.FilePath)).ConfigureAwait(true);
            SetCurrentCover(art, $"No embedded cover on {selectedTrack.FileName}.");
        }
        catch (Exception ex)
        {
            SetCurrentCover(null, "Unable to read album art for selected track.");
            StatusMessage = $"Failed to read album art: {ex.Message}";
        }
    }

    public async Task<bool> SetCoverForSelectionAsync(string imagePath)
    {
        if (IsBusy)
        {
            return false;
        }

        var selectedTracks = _selectedTracks
            .OrderBy(track => track.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedTracks.Length == 0)
        {
            StatusMessage = "Select one or more tracks before setting album art.";
            return false;
        }

        AlbumArt art;
        try
        {
            art = await LoadAlbumArtFromImageAsync(imagePath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }

        IsBusy = true;
        StatusMessage = $"Embedding cover into {selectedTracks.Length} selected track(s)...";

        try
        {
            var updatedCount = 0;
            var errors = new List<string>();

            await Task.Run(() =>
            {
                foreach (var track in selectedTracks)
                {
                    try
                    {
                        _artService.SetPrimary(track.FilePath, art);
                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{track.FileName}: {ex.Message}");
                    }
                }
            }).ConfigureAwait(true);

            if (errors.Count == 0)
            {
                StatusMessage = $"Embedded cover into {updatedCount} track(s).";
            }
            else
            {
                StatusMessage = $"Embedded cover into {updatedCount} track(s); {errors.Count} failed.";
            }

            await RefreshSelectedCoverAsync().ConfigureAwait(true);
            return errors.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> RemoveCoverFromSelectionAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        var selectedTracks = _selectedTracks
            .OrderBy(track => track.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedTracks.Length == 0)
        {
            StatusMessage = "Select one or more tracks before removing album art.";
            return false;
        }

        IsBusy = true;
        StatusMessage = $"Removing cover art from {selectedTracks.Length} selected track(s)...";

        try
        {
            var updatedCount = 0;
            var errors = new List<string>();

            await Task.Run(() =>
            {
                foreach (var track in selectedTracks)
                {
                    try
                    {
                        _artService.Remove(track.FilePath);
                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{track.FileName}: {ex.Message}");
                    }
                }
            }).ConfigureAwait(true);

            if (errors.Count == 0)
            {
                StatusMessage = $"Removed cover art from {updatedCount} track(s).";
            }
            else
            {
                StatusMessage = $"Removed cover art from {updatedCount} track(s); {errors.Count} failed.";
            }

            await RefreshSelectedCoverAsync().ConfigureAwait(true);
            return errors.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<string?> ExtractCoverFromSelectionAsync(string outputPath)
    {
        if (IsBusy)
        {
            return null;
        }

        var selectedTrack = GetPrimarySelectedTrack();
        if (selectedTrack is null)
        {
            StatusMessage = "Select a track before extracting album art.";
            return null;
        }

        IsBusy = true;
        StatusMessage = $"Extracting cover from {selectedTrack.FileName}...";

        try
        {
            var extractedPath = await Task.Run(() => _artService.ExtractPrimary(selectedTrack.FilePath, outputPath)).ConfigureAwait(true);
            StatusMessage = $"Extracted cover to {extractedPath}.";
            return extractedPath;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to extract cover: {ex.Message}";
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ApplyCoverToSelectedFolderAsync(string imagePath)
    {
        if (IsBusy)
        {
            return false;
        }

        var selectedTrack = GetPrimarySelectedTrack();
        if (selectedTrack is null)
        {
            StatusMessage = "Select a track before applying a folder-wide cover.";
            return false;
        }

        var folderPath = Path.GetDirectoryName(selectedTrack.FilePath);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusMessage = "Selected track does not have a valid folder path.";
            return false;
        }

        AlbumArt art;
        try
        {
            art = await LoadAlbumArtFromImageAsync(imagePath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return false;
        }

        IsBusy = true;
        StatusMessage = $"Applying cover to all tracks in {folderPath}...";

        try
        {
            var updatedCount = await Task.Run(() => _artService.ApplyPrimaryToFolder(folderPath, art)).ConfigureAwait(true);
            StatusMessage = $"Applied cover to {updatedCount} track(s) in {folderPath}.";
            await RefreshSelectedCoverAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed folder-wide cover apply: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private TrackRowViewModel[] GetTracksForRenameScope()
    {
        return _selectedTracks.Count > 0
            ? _selectedTracks
                .OrderBy(track => track.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Tracks
                .OrderBy(track => track.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private void SetRenamePlan(IReadOnlyList<RenamePlanEntry> renamePlan)
    {
        _renamePlan = renamePlan;
        RaisePropertyChanged(nameof(HasRenamePlan));
    }

    private void UpdateTrackPaths(IReadOnlyList<RenamePlanEntry> plan)
    {
        if (plan.Count == 0)
        {
            return;
        }

        var trackByPath = Tracks.ToDictionary(track => track.FilePath, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in plan)
        {
            if (!entry.WillRename)
            {
                continue;
            }

            if (trackByPath.TryGetValue(entry.OriginalPath, out var track))
            {
                track.UpdateFilePath(entry.TargetPath);
            }
        }

        if (!string.IsNullOrWhiteSpace(_currentFolderPath) && Directory.Exists(_currentFolderPath))
        {
            _currentFolderPath = Path.GetFullPath(_currentFolderPath);
        }
    }

    private static bool ApplyFilenameSuggestion(TrackRowViewModel track, FilenameTagSuggestion suggestion)
    {
        var appliedAny = false;

        if (suggestion.SuggestedTags.TrackNumber.HasValue)
        {
            track.TrackNumber = suggestion.SuggestedTags.TrackNumber;
            appliedAny = true;
        }

        if (!string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Artist))
        {
            track.Artist = suggestion.SuggestedTags.Artist;
            appliedAny = true;
        }

        if (!string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Title))
        {
            track.Title = suggestion.SuggestedTags.Title;
            appliedAny = true;
        }

        return appliedAny;
    }

    private TrackRowViewModel? GetPrimarySelectedTrack()
    {
        return Tracks.FirstOrDefault(track => _selectedTracks.Contains(track));
    }

    private async Task<AlbumArt> LoadAlbumArtFromImageAsync(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Image file does not exist: {imagePath}");
        }

        var mimeType = ParseImageMimeType(imagePath);
        var bytes = await File.ReadAllBytesAsync(imagePath).ConfigureAwait(true);
        return new AlbumArt(mimeType, bytes, AlbumArtKind.FrontCover, "TuneTag cover");
    }

    private void SetCurrentCover(AlbumArt? art, string summaryWhenMissing)
    {
        _currentCover = art;

        var nextPreview = art is null ? null : TryCreateBitmap(art.Bytes);
        var previousPreview = _currentCoverPreview;

        if (!ReferenceEquals(previousPreview, nextPreview))
        {
            CurrentCoverPreview = nextPreview;
            previousPreview?.Dispose();
        }

        CurrentCoverSummary = art is null
            ? summaryWhenMissing
            : $"{art.MimeType} • {art.Bytes.Length:N0} bytes";

        RaisePropertyChanged(nameof(HasCurrentCover));
    }

    private static Bitmap? TryCreateBitmap(byte[] imageBytes)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static string ParseImageMimeType(string imagePath)
    {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => throw new InvalidOperationException("Supported cover image formats are: .jpg, .jpeg, .png, .webp, .gif, .bmp")
        };
    }

    private void ReplaceTracks(IEnumerable<TrackRowViewModel> tracks)
    {
        foreach (var track in Tracks)
        {
            track.PropertyChanged -= TrackOnPropertyChanged;
        }

        Tracks.Clear();

        foreach (var track in tracks)
        {
            track.PropertyChanged += TrackOnPropertyChanged;
            Tracks.Add(track);
        }

        RaisePropertyChanged(nameof(SelectionSummary));
    }

    private void TrackOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrackRowViewModel.IsDirty))
        {
            RefreshTrackStateIndicators();
        }
    }

    private void RefreshTrackStateIndicators()
    {
        RaisePropertyChanged(nameof(DirtyTrackCount));
        RaisePropertyChanged(nameof(HasDirtyTracks));
        RaisePropertyChanged(nameof(SelectionSummary));
    }
}
