using TuneTag.Core.Formats;
using TuneTag.Core.Models;
using TuneTag.Core.Services;

namespace TuneTag.Core.Tests;

public sealed class TagLibArtServiceTests
{
    public static TheoryData<string, string, string> RoundTripCases => new()
    {
        { "sample.mp3", "image/jpeg", OnePixelJpegBase64 },
        { "sample.mp3", "image/png", OnePixelPngBase64 },
        { "sample.flac", "image/jpeg", OnePixelJpegBase64 },
        { "sample.flac", "image/png", OnePixelPngBase64 },
        { "sample.m4a", "image/jpeg", OnePixelJpegBase64 },
        { "sample.m4a", "image/png", OnePixelPngBase64 }
    };

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void SetPrimary_ThenReadPrimary_RoundTripsForMp3FlacAndM4a(string fixtureName, string mimeType, string base64)
    {
        var service = CreateService();
        var workingCopy = CreateWorkingCopy(fixtureName);
        var imageBytes = Convert.FromBase64String(base64);

        service.SetPrimary(workingCopy, new AlbumArt(mimeType, imageBytes, AlbumArtKind.FrontCover, "test cover"));
        var roundTrip = service.ReadPrimary(workingCopy);

        Assert.NotNull(roundTrip);
        Assert.Equal(mimeType, roundTrip!.MimeType);
        Assert.Equal(AlbumArtKind.FrontCover, roundTrip.Kind);
        Assert.True(roundTrip.Bytes.SequenceEqual(imageBytes));
    }

    [Theory]
    [InlineData("sample.mp3", "image/jpeg", OnePixelJpegBase64, ".jpg")]
    [InlineData("sample.m4a", "image/png", OnePixelPngBase64, ".png")]
    public void ExtractPrimary_WritesExpectedExtensionAndBytes(string fixtureName, string mimeType, string base64, string expectedExtension)
    {
        var service = CreateService();
        var workingCopy = CreateWorkingCopy(fixtureName);
        var imageBytes = Convert.FromBase64String(base64);

        service.SetPrimary(workingCopy, new AlbumArt(mimeType, imageBytes));

        var outputRoot = Path.Combine(Path.GetTempPath(), $"tunetag-art-{Guid.NewGuid():N}", "cover");
        var extractedPath = service.ExtractPrimary(workingCopy, outputRoot);

        Assert.EndsWith(expectedExtension, extractedPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(extractedPath));

        var extractedBytes = File.ReadAllBytes(extractedPath);
        Assert.True(extractedBytes.SequenceEqual(imageBytes));
    }

    [Theory]
    [InlineData("sample.mp3")]
    [InlineData("sample.flac")]
    [InlineData("sample.m4a")]
    public void Remove_ClearsEmbeddedArtWithoutResidualPictures(string fixtureName)
    {
        var service = CreateService();
        var workingCopy = CreateWorkingCopy(fixtureName);
        var imageBytes = Convert.FromBase64String(OnePixelJpegBase64);

        service.SetPrimary(workingCopy, new AlbumArt("image/jpeg", imageBytes));
        service.Remove(workingCopy);

        Assert.Null(service.ReadPrimary(workingCopy));

        using var tagFile = TagLib.File.Create(workingCopy);
        Assert.Empty(tagFile.Tag.Pictures);
    }

    [Fact]
    public void ApplyPrimaryToFolder_SetsSameCoverAcrossAllSupportedTracks()
    {
        var service = CreateService();
        var workingFolder = Path.Combine(Path.GetTempPath(), $"tunetag-art-folder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingFolder);

        var fixtureNames = new[] { "sample.mp3", "sample.flac", "sample.m4a" };
        var copiedFiles = fixtureNames
            .Select(name =>
            {
                var destination = Path.Combine(workingFolder, name);
                File.Copy(GetFixturePath(name), destination, overwrite: true);
                return destination;
            })
            .ToArray();

        File.WriteAllText(Path.Combine(workingFolder, "ignore.txt"), "not audio");

        var imageBytes = Convert.FromBase64String(OnePixelPngBase64);
        var updatedCount = service.ApplyPrimaryToFolder(workingFolder, new AlbumArt("image/png", imageBytes));

        Assert.Equal(3, updatedCount);

        foreach (var filePath in copiedFiles)
        {
            var art = service.ReadPrimary(filePath);
            Assert.NotNull(art);
            Assert.Equal("image/png", art!.MimeType);
            Assert.True(art.Bytes.SequenceEqual(imageBytes));
        }
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
        var sourcePath = GetFixturePath(fixtureName);
        var destinationPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{fixtureName}");
        File.Copy(sourcePath, destinationPath, overwrite: true);
        return destinationPath;
    }

    private static string GetFixturePath(string fixtureName)
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        Assert.True(File.Exists(sourcePath), $"Missing fixture file: {sourcePath}");
        return sourcePath;
    }

    private const string OnePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Z3ioAAAAASUVORK5CYII=";

    private const string OnePixelJpegBase64 = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxAQEBUQEBIVFRUVFRUVFRUVFRUVFRUWFhUVFRUYHSggGBolGxUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGhAQGi0lHyUtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAAEAAQMBIgACEQEDEQH/xAAXAAEBAQEAAAAAAAAAAAAAAAABAgAD/8QAFhEBAQEAAAAAAAAAAAAAAAAAAAER/8QAFgEBAQEAAAAAAAAAAAAAAAAAAQAC/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAwDAQACEQMRAD8A9wD/AP/Z";
}
