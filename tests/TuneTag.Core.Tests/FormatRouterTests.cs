using TuneTag.Core.Formats;

namespace TuneTag.Core.Tests;

public sealed class FormatRouterTests
{
    [Theory]
    [InlineData("track.mp3", "MP3")]
    [InlineData("track.flac", "FLAC")]
    [InlineData("track.ogg", "Ogg Vorbis")]
    [InlineData("track.m4a", "MP4/M4A")]
    public void Resolve_UsesExtensionForKnownTypes(string fileName, string expectedFormat)
    {
        var router = CreateRouter();
        var filePath = CreateTempFile(fileName, []);

        var resolved = router.Resolve(filePath);

        Assert.Equal(expectedFormat, resolved.Name);
    }

    [Theory]
    [InlineData("mp3.bin", new byte[] { 0x49, 0x44, 0x33, 0x04 })]
    [InlineData("flac.bin", new byte[] { 0x66, 0x4C, 0x61, 0x43 })]
    [InlineData("m4a.bin", new byte[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20 })]
    public void Resolve_FallsBackToMagicBytes(string fileName, byte[] header)
    {
        var router = CreateRouter();
        var filePath = CreateTempFile(fileName, header);

        var resolved = router.Resolve(filePath);

        Assert.NotNull(resolved);
    }

    [Fact]
    public void Resolve_ThrowsClearErrorForUnknownType()
    {
        var router = CreateRouter();
        var filePath = CreateTempFile("not-audio.xyz", new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var ex = Assert.Throws<UnsupportedAudioFormatException>(() => router.Resolve(filePath));

        Assert.Contains("Unsupported audio format", ex.Message);
        Assert.Contains(".xyz", ex.Message);
    }

    private static FormatRouter CreateRouter()
    {
        return new FormatRouter(
        [
            new Mp3AudioFileFormat(),
            new FlacAudioFileFormat(),
            new OggAudioFileFormat(),
            new Mp4AudioFileFormat()
        ]);
    }

    private static string CreateTempFile(string fileName, byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{fileName}");
        File.WriteAllBytes(path, content);
        return path;
    }
}
