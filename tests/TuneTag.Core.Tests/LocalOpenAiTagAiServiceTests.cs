using System.Net;
using System.Text;
using System.Text.Json;
using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.Core.Tests;

public sealed class LocalOpenAiTagAiServiceTests
{
    [Fact]
    public async Task SuggestMissingTagsAsync_SendsOnlyChosenContextFields_AndParsesSuggestions()
    {
        string? capturedRequestBody = null;

        var handler = new StubHttpMessageHandler(async request =>
        {
            if (request.RequestUri is null)
            {
                throw new InvalidOperationException("Missing request URI.");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                capturedRequestBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync();

                const string responseJson = """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"suggestions\":[{\"id\":\"track-1\",\"title\":\"Harder Better Faster Stronger\",\"genre\":\"French House\",\"reason\":\"Commonly tagged this way\"}]}"
                      }
                    }
                  ]
                }
                """;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            }

            throw new InvalidOperationException($"Unexpected endpoint: {request.RequestUri}");
        });

        var service = new LocalOpenAiTagAiService(new HttpClient(handler));

        var options = new TagAiRequestOptions(
            "http://127.0.0.1:11434/v1",
            "llama3.2",
            new HashSet<TagMetadataField> { TagMetadataField.Artist },
            new HashSet<TagMetadataField> { TagMetadataField.Title, TagMetadataField.Genre },
            TimeSpan.FromSeconds(5));

        var suggestions = await service.SuggestMissingTagsAsync(
        [
            new TagAiTrackInput(
                "/music/track1.mp3",
                new TrackTags
                {
                    Artist = "Daft Punk",
                    Album = "Discovery",
                    AlbumArtist = "Daft Punk"
                })
        ],
        options);

        Assert.NotNull(capturedRequestBody);

        using var requestDoc = JsonDocument.Parse(capturedRequestBody!);
        var userMessage = requestDoc.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .Last()
            .GetProperty("content")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(userMessage));

        using var userDoc = JsonDocument.Parse(userMessage!);
        var tracks = userDoc.RootElement.GetProperty("tracks");
        var firstTrack = tracks.EnumerateArray().Single();
        var context = firstTrack.GetProperty("context");

        Assert.True(context.TryGetProperty("artist", out var artist));
        Assert.Equal("Daft Punk", artist.GetString());
        Assert.False(context.TryGetProperty("album", out _));
        Assert.False(context.TryGetProperty("albumArtist", out _));

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("/music/track1.mp3", suggestion.FilePath);
        Assert.Equal("Harder Better Faster Stronger", suggestion.SuggestedTags.Title);
        Assert.Equal("French House", suggestion.SuggestedTags.Genre);
        Assert.Equal("Commonly tagged this way", suggestion.Reason);
    }

    [Fact]
    public async Task ProbeAsync_WhenEndpointUnreachable_ReturnsGracefulFailure()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused")));
        var service = new LocalOpenAiTagAiService(new HttpClient(handler));

        var result = await service.ProbeAsync(TagAiRequestOptions.Default);

        Assert.False(result.IsReachable);
        Assert.Contains("Could not reach local model endpoint", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responder(request);
        }
    }
}
