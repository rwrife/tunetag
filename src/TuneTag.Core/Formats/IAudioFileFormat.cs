namespace TuneTag.Core.Formats;

public interface IAudioFileFormat
{
    string Name { get; }

    IReadOnlyCollection<string> SupportedExtensions { get; }

    bool SupportsExtension(string extension);

    bool SupportsMagicBytes(ReadOnlySpan<byte> header);
}
