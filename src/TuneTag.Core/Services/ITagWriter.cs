using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public interface ITagWriter
{
    void Write(string filePath, TrackTags tags);
}
