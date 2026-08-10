namespace TuneTag.Core.Formats;

public sealed class Mp3AudioFileFormat : AudioFileFormatBase
{
    public Mp3AudioFileFormat()
        : base("MP3", ".mp3")
    {
    }

    public override bool SupportsMagicBytes(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 &&
            header[0] == (byte)'I' &&
            header[1] == (byte)'D' &&
            header[2] == (byte)'3')
        {
            return true;
        }

        return header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0;
    }
}
