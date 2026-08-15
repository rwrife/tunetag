using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _aiAssistEnabled;
    private string _aiEndpoint = TagAiRequestOptions.Default.EndpointBaseUrl;
    private string _aiModel = TagAiRequestOptions.Default.Model;
    private bool _aiShareArtist = true;
    private bool _aiShareAlbum = true;
    private bool _aiShareAlbumArtist = true;
    private bool _aiSuggestTitle = true;
    private bool _aiSuggestAlbum = true;
    private bool _aiSuggestGenre = true;
    private bool _aiServiceReachable;
    private string _aiAssistStatus = "Local AI assist is off by default. Enable it to configure a localhost endpoint.";
    private string _aiProposalText = "No AI suggestions yet.";
    private readonly Dictionary<string, TagAiSuggestion> _pendingAiSuggestions = new(StringComparer.OrdinalIgnoreCase);

    public ITagAiService TagAiService { get; set; } = new DisabledTagAiService();

    public bool AiAssistEnabled
    {
        get => _aiAssistEnabled;
        set
        {
            if (!SetProperty(ref _aiAssistEnabled, value))
            {
                return;
            }

            if (!value)
            {
                _aiServiceReachable = false;
                _pendingAiSuggestions.Clear();
                AiProposalText = "No AI suggestions yet.";
                AiAssistStatus = "Local AI assist is off by default. Enable it to configure a localhost endpoint.";
            }
            else
            {
                _aiServiceReachable = false;
                _pendingAiSuggestions.Clear();
                AiProposalText = "No AI suggestions yet.";
                AiAssistStatus = "AI enabled. Probe the local endpoint before requesting suggestions.";
            }

            RaisePropertyChanged(nameof(CanRequestAiSuggestions));
            RaisePropertyChanged(nameof(HasPendingAiSuggestions));
        }
    }

    public string AiEndpoint
    {
        get => _aiEndpoint;
        set => SetProperty(ref _aiEndpoint, value);
    }

    public string AiModel
    {
        get => _aiModel;
        set => SetProperty(ref _aiModel, value);
    }

    public bool AiShareArtist
    {
        get => _aiShareArtist;
        set => SetProperty(ref _aiShareArtist, value);
    }

    public bool AiShareAlbum
    {
        get => _aiShareAlbum;
        set => SetProperty(ref _aiShareAlbum, value);
    }

    public bool AiShareAlbumArtist
    {
        get => _aiShareAlbumArtist;
        set => SetProperty(ref _aiShareAlbumArtist, value);
    }

    public bool AiSuggestTitle
    {
        get => _aiSuggestTitle;
        set => SetProperty(ref _aiSuggestTitle, value);
    }

    public bool AiSuggestAlbum
    {
        get => _aiSuggestAlbum;
        set => SetProperty(ref _aiSuggestAlbum, value);
    }

    public bool AiSuggestGenre
    {
        get => _aiSuggestGenre;
        set => SetProperty(ref _aiSuggestGenre, value);
    }

    public string AiAssistStatus
    {
        get => _aiAssistStatus;
        private set => SetProperty(ref _aiAssistStatus, value);
    }

    public string AiProposalText
    {
        get => _aiProposalText;
        private set => SetProperty(ref _aiProposalText, value);
    }

    public bool CanRequestAiSuggestions => AiAssistEnabled && _aiServiceReachable;

    public bool HasPendingAiSuggestions => _pendingAiSuggestions.Count > 0;

    public async Task<bool> ProbeAiServiceAsync(CancellationToken cancellationToken = default)
    {
        if (!AiAssistEnabled)
        {
            AiAssistStatus = "AI assist is disabled. Turn it on first.";
            return false;
        }

        TagAiRequestOptions options;
        try
        {
            options = BuildAiRequestOptions();
        }
        catch (Exception ex)
        {
            AiAssistStatus = ex.Message;
            return false;
        }

        var probe = await TagAiService.ProbeAsync(options, cancellationToken).ConfigureAwait(true);
        _aiServiceReachable = probe.IsReachable;
        AiAssistStatus = probe.Message;

        if (!probe.IsReachable)
        {
            _pendingAiSuggestions.Clear();
            AiProposalText = "AI suggestions are unavailable because the local model endpoint could not be reached.";
        }

        RaisePropertyChanged(nameof(CanRequestAiSuggestions));
        RaisePropertyChanged(nameof(HasPendingAiSuggestions));
        return probe.IsReachable;
    }

    public async Task<int> GenerateAiSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        if (!AiAssistEnabled)
        {
            AiAssistStatus = "AI assist is disabled. Turn it on first.";
            return 0;
        }

        var tracks = GetTracksForRenameScope();
        if (tracks.Length == 0)
        {
            AiAssistStatus = "Load tracks (or select tracks) before asking for AI suggestions.";
            return 0;
        }

        if (!_aiServiceReachable && !await ProbeAiServiceAsync(cancellationToken).ConfigureAwait(true))
        {
            return 0;
        }

        TagAiRequestOptions options;
        try
        {
            options = BuildAiRequestOptions();
        }
        catch (Exception ex)
        {
            AiAssistStatus = ex.Message;
            return 0;
        }

        var inputs = tracks
            .Select(track => new TagAiTrackInput(track.FilePath, track.ToTrackTags()))
            .ToArray();

        var suggestions = await TagAiService.SuggestMissingTagsAsync(inputs, options, cancellationToken).ConfigureAwait(true);

        _pendingAiSuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            _pendingAiSuggestions[suggestion.FilePath] = suggestion;
        }

        RaisePropertyChanged(nameof(HasPendingAiSuggestions));

        if (_pendingAiSuggestions.Count == 0)
        {
            AiAssistStatus = "No missing-field suggestions were returned for the current selection.";
            AiProposalText = "No AI suggestions yet.";
            return 0;
        }

        var lines = tracks
            .Where(track => _pendingAiSuggestions.ContainsKey(track.FilePath))
            .Select(track =>
            {
                var suggestion = _pendingAiSuggestions[track.FilePath];
                var changes = new List<string>();

                if (!string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Title))
                {
                    changes.Add($"title → {suggestion.SuggestedTags.Title}");
                }

                if (!string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Album))
                {
                    changes.Add($"album → {suggestion.SuggestedTags.Album}");
                }

                if (!string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Genre))
                {
                    changes.Add($"genre → {suggestion.SuggestedTags.Genre}");
                }

                var reason = string.IsNullOrWhiteSpace(suggestion.Reason)
                    ? string.Empty
                    : $" ({suggestion.Reason})";

                return $"{Path.GetFileName(track.FilePath)}: {string.Join(", ", changes)}{reason}";
            })
            .ToArray();

        AiProposalText = string.Join(Environment.NewLine, lines);
        AiAssistStatus = $"Generated {_pendingAiSuggestions.Count} AI suggestion(s). Review and click Apply to write them.";
        return _pendingAiSuggestions.Count;
    }

    public int ApplyAiSuggestions()
    {
        if (_pendingAiSuggestions.Count == 0)
        {
            AiAssistStatus = "No pending AI suggestions to apply.";
            return 0;
        }

        var updatedTracks = 0;

        foreach (var track in Tracks)
        {
            if (!_pendingAiSuggestions.TryGetValue(track.FilePath, out var suggestion))
            {
                continue;
            }

            var appliedAny = false;

            if (string.IsNullOrWhiteSpace(track.Title) && !string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Title))
            {
                track.Title = suggestion.SuggestedTags.Title;
                appliedAny = true;
            }

            if (string.IsNullOrWhiteSpace(track.Album) && !string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Album))
            {
                track.Album = suggestion.SuggestedTags.Album;
                appliedAny = true;
            }

            if (string.IsNullOrWhiteSpace(track.Genre) && !string.IsNullOrWhiteSpace(suggestion.SuggestedTags.Genre))
            {
                track.Genre = suggestion.SuggestedTags.Genre;
                appliedAny = true;
            }

            if (appliedAny)
            {
                updatedTracks++;
            }
        }

        _pendingAiSuggestions.Clear();
        RaisePropertyChanged(nameof(HasPendingAiSuggestions));

        AiProposalText = "No AI suggestions yet.";

        if (updatedTracks == 0)
        {
            AiAssistStatus = "No suggestions were applicable (fields may already be filled).";
            return 0;
        }

        AiAssistStatus = $"Applied AI suggestions to {updatedTracks} track(s). Review and Save to persist.";
        RefreshTrackStateIndicators();
        RefreshRenamePreview();
        return updatedTracks;
    }

    private TagAiRequestOptions BuildAiRequestOptions()
    {
        if (string.IsNullOrWhiteSpace(AiEndpoint))
        {
            throw new InvalidOperationException("AI endpoint is required (example: http://127.0.0.1:11434/v1).");
        }

        if (string.IsNullOrWhiteSpace(AiModel))
        {
            throw new InvalidOperationException("AI model is required.");
        }

        var context = new HashSet<TagMetadataField>();
        if (AiShareArtist)
        {
            context.Add(TagMetadataField.Artist);
        }

        if (AiShareAlbum)
        {
            context.Add(TagMetadataField.Album);
        }

        if (AiShareAlbumArtist)
        {
            context.Add(TagMetadataField.AlbumArtist);
        }

        var targets = new HashSet<TagMetadataField>();
        if (AiSuggestTitle)
        {
            targets.Add(TagMetadataField.Title);
        }

        if (AiSuggestAlbum)
        {
            targets.Add(TagMetadataField.Album);
        }

        if (AiSuggestGenre)
        {
            targets.Add(TagMetadataField.Genre);
        }

        if (targets.Count == 0)
        {
            throw new InvalidOperationException("Select at least one target field (title/album/genre) for AI suggestions.");
        }

        return new TagAiRequestOptions(
            AiEndpoint.Trim(),
            AiModel.Trim(),
            context,
            targets,
            TimeSpan.FromSeconds(20));
    }

    private sealed class DisabledTagAiService : ITagAiService
    {
        public Task<TagAiProbeResult> ProbeAsync(TagAiRequestOptions options, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TagAiProbeResult(false, "No AI service configured."));
        }

        public Task<IReadOnlyList<TagAiSuggestion>> SuggestMissingTagsAsync(
            IReadOnlyList<TagAiTrackInput> tracks,
            TagAiRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TagAiSuggestion>>([]);
        }
    }
}
