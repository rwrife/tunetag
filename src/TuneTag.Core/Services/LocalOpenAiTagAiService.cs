using System.Net.Http.Json;
using System.Text.Json;
using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public sealed class LocalOpenAiTagAiService : ITagAiService
{
    private const string SystemPrompt = "You are helping with music metadata cleanup. Return JSON only: {\"suggestions\":[{\"id\":\"track-id\",\"title\":\"...\",\"album\":\"...\",\"genre\":\"...\",\"reason\":\"...\"}]}. Only provide requested missing fields.";

    private readonly HttpClient _httpClient;

    public LocalOpenAiTagAiService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<TagAiProbeResult> ProbeAsync(TagAiRequestOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var modelsUri = BuildEndpoint(options.EndpointBaseUrl, "models");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        try
        {
            using var response = await _httpClient.GetAsync(modelsUri, timeoutCts.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new TagAiProbeResult(true, $"Local model endpoint is reachable at {modelsUri}.");
            }

            return new TagAiProbeResult(false, $"Local model endpoint responded with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }
        catch (Exception ex)
        {
            return new TagAiProbeResult(false, $"Could not reach local model endpoint: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<TagAiSuggestion>> SuggestMissingTagsAsync(
        IReadOnlyList<TagAiTrackInput> tracks,
        TagAiRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(options);

        var prepared = PrepareTrackPayload(tracks, options.TargetFields, options.ContextFields);
        if (prepared.Count == 0)
        {
            return [];
        }

        var payload = new
        {
            model = options.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new
                    {
                        context_fields = options.ContextFields.Select(FieldName).ToArray(),
                        target_fields = options.TargetFields.Select(FieldName).ToArray(),
                        tracks = prepared.Select(item => item.Payload).ToArray()
                    })
                }
            }
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        var completionUri = BuildEndpoint(options.EndpointBaseUrl, "chat/completions");
        using var response = await _httpClient.PostAsJsonAsync(completionUri, payload, timeoutCts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
        using var root = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token).ConfigureAwait(false);

        var content = ExtractMessageContent(root.RootElement);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        using var modelJson = JsonDocument.Parse(content);
        if (!modelJson.RootElement.TryGetProperty("suggestions", out var suggestionsElement) || suggestionsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var preparedLookup = prepared.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var results = new List<TagAiSuggestion>();

        foreach (var suggestion in suggestionsElement.EnumerateArray())
        {
            if (!suggestion.TryGetProperty("id", out var idElement))
            {
                continue;
            }

            var id = idElement.GetString();
            if (string.IsNullOrWhiteSpace(id) || !preparedLookup.TryGetValue(id, out var mapped))
            {
                continue;
            }

            var tags = new TrackTags
            {
                Title = ReadSuggestedText(suggestion, "title"),
                Album = ReadSuggestedText(suggestion, "album"),
                Genre = ReadSuggestedText(suggestion, "genre")
            };

            if (string.IsNullOrWhiteSpace(tags.Title)
                && string.IsNullOrWhiteSpace(tags.Album)
                && string.IsNullOrWhiteSpace(tags.Genre))
            {
                continue;
            }

            var reason = ReadSuggestedText(suggestion, "reason");
            results.Add(new TagAiSuggestion(mapped.FilePath, tags, reason));
        }

        return results;
    }

    private static List<PreparedTrackPayload> PrepareTrackPayload(
        IReadOnlyList<TagAiTrackInput> tracks,
        IReadOnlySet<TagMetadataField> targetFields,
        IReadOnlySet<TagMetadataField> contextFields)
    {
        var prepared = new List<PreparedTrackPayload>();

        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var missingTargets = targetFields
                .Where(field => IsMissing(track.Tags, field))
                .Select(FieldName)
                .ToArray();

            if (missingTargets.Length == 0)
            {
                continue;
            }

            var context = BuildContext(track.Tags, contextFields);
            var id = $"track-{i + 1}";

            prepared.Add(new PreparedTrackPayload(
                id,
                track.FilePath,
                new
                {
                    id,
                    context,
                    missing_targets = missingTargets
                }));
        }

        return prepared;
    }

    private static Dictionary<string, string> BuildContext(TrackTags tags, IReadOnlySet<TagMetadataField> fields)
    {
        var context = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            var value = field switch
            {
                TagMetadataField.Title => tags.Title,
                TagMetadataField.Artist => tags.Artist,
                TagMetadataField.Album => tags.Album,
                TagMetadataField.AlbumArtist => tags.AlbumArtist,
                TagMetadataField.Genre => tags.Genre,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                context[FieldName(field)] = value.Trim();
            }
        }

        return context;
    }

    private static bool IsMissing(TrackTags tags, TagMetadataField field)
    {
        return field switch
        {
            TagMetadataField.Title => string.IsNullOrWhiteSpace(tags.Title),
            TagMetadataField.Album => string.IsNullOrWhiteSpace(tags.Album),
            TagMetadataField.Genre => string.IsNullOrWhiteSpace(tags.Genre),
            TagMetadataField.Artist => string.IsNullOrWhiteSpace(tags.Artist),
            TagMetadataField.AlbumArtist => string.IsNullOrWhiteSpace(tags.AlbumArtist),
            _ => true
        };
    }

    private static string FieldName(TagMetadataField field)
    {
        return field switch
        {
            TagMetadataField.Title => "title",
            TagMetadataField.Artist => "artist",
            TagMetadataField.Album => "album",
            TagMetadataField.AlbumArtist => "albumArtist",
            TagMetadataField.Genre => "genre",
            _ => field.ToString().ToLowerInvariant()
        };
    }

    private static string? ReadSuggestedText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        var value = valueElement.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ExtractMessageContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message))
            {
                continue;
            }

            if (message.TryGetProperty("content", out var contentElement))
            {
                if (contentElement.ValueKind == JsonValueKind.String)
                {
                    return contentElement.GetString();
                }

                if (contentElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in contentElement.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                        {
                            return textElement.GetString();
                        }
                    }
                }
            }
        }

        return null;
    }

    private static Uri BuildEndpoint(string baseUrl, string suffix)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Endpoint URL is required.", nameof(baseUrl));
        }

        var normalizedBase = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";
        return new Uri(new Uri(normalizedBase, UriKind.Absolute), suffix);
    }

    private sealed record PreparedTrackPayload(string Id, string FilePath, object Payload);
}
