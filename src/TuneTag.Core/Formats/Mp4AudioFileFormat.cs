namespace TuneTag.Core.Formats;

public sealed class Mp4AudioFileFormat : AudioFileFormatBase
{
    public Mp4AudioFileFormat()
        : base("MP4/M4A", ".m4a", ".mp4", ".m4b", ".aac", ".alac")
    {
    }

    public override bool SupportsMagicBytes(ReadOnlySpan<byte> header)
    {
        return header.Length >= 8 &&
               header[4] == (byte)'f' &&
               header[5] == (byte)'t' &&
               header[6] == (byte)'y' &&
               header[7] == (byte)'p';
    }
}
