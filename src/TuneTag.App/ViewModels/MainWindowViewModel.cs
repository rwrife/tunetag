using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TuneTag.App.Services;

namespace TuneTag.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ITrackLibraryService _trackLibraryService;
    private readonly HashSet<TrackRowViewModel> _selectedTracks = [];

    private BatchEditFieldOption? _selectedBatchEditField;
    private string? _batchEditValue;
    private string _statusMessage = "Open a folder to begin editing tags.";
    private bool _isBusy;
    private int _selectedTrackCount;

    public MainWindowViewModel(ITrackLibraryService trackLibraryService)
    {
        _trackLibraryService = trackLibraryService ?? throw new ArgumentNullException(nameof(trackLibraryService));

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
            }
        }
    }

    public int DirtyTrackCount => Tracks.Count(static track => track.IsDirty);

    public bool HasDirtyTracks => DirtyTrackCount > 0;

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
