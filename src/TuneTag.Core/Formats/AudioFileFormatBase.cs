namespace TuneTag.Core.Formats;

public abstract class AudioFileFormatBase : IAudioFileFormat
{
    private readonly HashSet<string> _extensions;

    protected AudioFileFormatBase(string name, params string[] extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (extensions is null || extensions.Length == 0)
        {
            throw new ArgumentException("At least one extension is required.", nameof(extensions));
        }

        Name = name;
        _extensions = new HashSet<string>(extensions.Select(NormalizeExtension), StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    public IReadOnlyCollection<string> SupportedExtensions => _extensions;

    public bool SupportsExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return _extensions.Contains(NormalizeExtension(extension));
    }

    public abstract bool SupportsMagicBytes(ReadOnlySpan<byte> header);

    private static string NormalizeExtension(string extension)
    {
        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
