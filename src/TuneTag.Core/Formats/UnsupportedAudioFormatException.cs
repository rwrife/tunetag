namespace TuneTag.Core.Formats;

public sealed class UnsupportedAudioFormatException : InvalidOperationException
{
    public UnsupportedAudioFormatException(string message)
        : base(message)
    {
    }
}
