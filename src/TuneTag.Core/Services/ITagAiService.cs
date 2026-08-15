using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public interface ITagAiService
{
    Task<TagAiProbeResult> ProbeAsync(TagAiRequestOptions options, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagAiSuggestion>> SuggestMissingTagsAsync(
        IReadOnlyList<TagAiTrackInput> tracks,
        TagAiRequestOptions options,
        CancellationToken cancellationToken = default);
}
