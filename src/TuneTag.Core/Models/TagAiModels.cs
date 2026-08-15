namespace TuneTag.Core.Models;

public sealed record TagAiTrackInput(string FilePath, TrackTags Tags);

public sealed record TagAiSuggestion(string FilePath, TrackTags SuggestedTags, string? Reason = null);

public sealed record TagAiProbeResult(bool IsReachable, string Message);

public sealed record TagAiRequestOptions(
    string EndpointBaseUrl,
    string Model,
    IReadOnlySet<TagMetadataField> ContextFields,
    IReadOnlySet<TagMetadataField> TargetFields,
    TimeSpan Timeout)
{
    public static TagAiRequestOptions Default { get; } = new(
        "http://127.0.0.1:11434/v1",
        "llama3.2",
        new HashSet<TagMetadataField>
        {
            TagMetadataField.Artist,
            TagMetadataField.Album,
            TagMetadataField.AlbumArtist
        },
        new HashSet<TagMetadataField>
        {
            TagMetadataField.Title,
            TagMetadataField.Album,
            TagMetadataField.Genre
        },
        TimeSpan.FromSeconds(20));
}
