namespace TuneTag.Core.Formats;

public sealed class FlacAudioFileFormat : AudioFileFormatBase
{
    public FlacAudioFileFormat()
        : base("FLAC", ".flac")
    {
    }

    public override bool SupportsMagicBytes(ReadOnlySpan<byte> header)
    {
        return header.Length >= 4 &&
               header[0] == (byte)'f' &&
               header[1] == (byte)'L' &&
               header[2] == (byte)'a' &&
               header[3] == (byte)'C';
    }
}
