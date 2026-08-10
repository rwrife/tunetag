namespace TuneTag.Core.Formats;

public sealed class OggAudioFileFormat : AudioFileFormatBase
{
    public OggAudioFileFormat()
        : base("Ogg Vorbis", ".ogg")
    {
    }

    public override bool SupportsMagicBytes(ReadOnlySpan<byte> header)
    {
        return header.Length >= 4 &&
               header[0] == (byte)'O' &&
               header[1] == (byte)'g' &&
               header[2] == (byte)'g' &&
               header[3] == (byte)'S';
    }
}
