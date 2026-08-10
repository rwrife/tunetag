namespace TuneTag.Core.Formats;

public sealed class FormatRouter
{
    private const int HeaderSizeBytes = 64;
    private readonly IReadOnlyList<IAudioFileFormat> _formats;

    public FormatRouter(IEnumerable<IAudioFileFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        _formats = formats.ToList();

        if (_formats.Count == 0)
        {
            throw new ArgumentException("At least one audio file format is required.", nameof(formats));
        }
    }

    public IAudioFileFormat Resolve(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Audio file was not found.", filePath);
        }

        var extension = Path.GetExtension(filePath);
        var extensionMatch = _formats.FirstOrDefault(format => format.SupportsExtension(extension));
        if (extensionMatch is not null)
        {
            return extensionMatch;
        }

        var header = ReadHeader(filePath, HeaderSizeBytes);
        var magicMatch = _formats.FirstOrDefault(format => format.SupportsMagicBytes(header));
        if (magicMatch is not null)
        {
            return magicMatch;
        }

        var supported = string.Join(", ", _formats.SelectMany(format => format.SupportedExtensions).Distinct(StringComparer.OrdinalIgnoreCase));
        throw new UnsupportedAudioFormatException(
            $"Unsupported audio format for '{filePath}'. Extension '{extension}' is not supported and no known magic bytes were detected. Supported extensions: {supported}.");
    }

    private static byte[] ReadHeader(string filePath, int maxBytes)
    {
        using var stream = File.OpenRead(filePath);
        var buffer = new byte[maxBytes];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        if (bytesRead == buffer.Length)
        {
            return buffer;
        }

        return buffer[..bytesRead];
    }
}
