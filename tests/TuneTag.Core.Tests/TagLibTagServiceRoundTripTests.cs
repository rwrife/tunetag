using TuneTag.Core.Formats;
using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.Core.Tests;

public sealed class TagLibTagServiceRoundTripTests
{
    [Theory]
    [InlineData("sample.mp3")]
    [InlineData("sample.flac")]
    [InlineData("sample.m4a")]
    public void Read_ReturnsNormalizedTrackTags(string fixtureName)
    {
        var service = CreateService();
        var workingCopy = CreateWorkingCopy(fixtureName);

        var tags = service.Read(workingCopy);

        Assert.NotNull(tags);
        Assert.NotNull(tags.RawFields);
        Assert.True(tags.RawFields.ContainsKey("format"));
    }

    [Theory]
    [InlineData("sample.mp3")]
    [InlineData("sample.flac")]
    [InlineData("sample.m4a")]
    public void Write_ThenRead_RoundTripsChanges(string fixtureName)
    {
        var service = CreateService();
        var workingCopy = CreateWorkingCopy(fixtureName);

        var updated = new TrackTags
        {
            Title = "Unit Test Title",
            Artist = "Unit Test Artist",
            Album = "Unit Test Album",
            AlbumArtist = "Unit Test Album Artist",
            TrackNumber = 3,
            DiscNumber = 1,
            Year = 2026,
            Genre = "Unit Test Genre",
            Composer = "Unit Test Composer",
            Comment = "Round-trip verification"
        };

        service.Write(workingCopy, updated);
        var roundTrip = service.Read(workingCopy);

        Assert.Equal(updated.Title, roundTrip.Title);
        Assert.Equal(updated.Artist, roundTrip.Artist);
        Assert.Equal(updated.Album, roundTrip.Album);
        Assert.Equal(updated.AlbumArtist, roundTrip.AlbumArtist);
        Assert.Equal(updated.TrackNumber, roundTrip.TrackNumber);
        Assert.Equal(updated.DiscNumber, roundTrip.DiscNumber);
        Assert.Equal(updated.Year, roundTrip.Year);
        Assert.Equal(updated.Genre, roundTrip.Genre);
        Assert.Equal(updated.Comment, roundTrip.Comment);
    }

    private static TagLibTagService CreateService()
    {
        return new TagLibTagService(
            new FormatRouter(
            [
                new Mp3AudioFileFormat(),
                new FlacAudioFileFormat(),
                new OggAudioFileFormat(),
                new Mp4AudioFileFormat()
            ]));
    }

    private static string CreateWorkingCopy(string fixtureName)
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        Assert.True(File.Exists(sourcePath), $"Missing fixture file: {sourcePath}");

        var destinationPath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}_{fixtureName}");

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return destinationPath;
    }
}
