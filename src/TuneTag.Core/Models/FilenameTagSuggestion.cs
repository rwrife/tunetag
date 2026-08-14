namespace TuneTag.Core.Models;

public sealed class FilenameTagSuggestion
{
    public FilenameTagSuggestion(TrackTags suggestedTags, IReadOnlyList<string> suggestedFields, string pattern)
    {
        SuggestedTags = suggestedTags ?? throw new ArgumentNullException(nameof(suggestedTags));
        SuggestedFields = suggestedFields ?? throw new ArgumentNullException(nameof(suggestedFields));
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    public TrackTags SuggestedTags { get; }

    public IReadOnlyList<string> SuggestedFields { get; }

    public string Pattern { get; }

    public bool HasSuggestions => SuggestedFields.Count > 0;
}
