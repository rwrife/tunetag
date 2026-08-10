namespace TuneTag.Core;

/// <summary>
/// Entry helpers for constructing core services with default format support.
/// </summary>
public static class TuneTagCore
{
    public static Services.TagLibTagService CreateDefaultTagService()
    {
        return new Services.TagLibTagService(
            new Formats.FormatRouter(
            [
                new Formats.Mp3AudioFileFormat(),
                new Formats.FlacAudioFileFormat(),
                new Formats.OggAudioFileFormat(),
                new Formats.Mp4AudioFileFormat()
            ]));
    }
}

