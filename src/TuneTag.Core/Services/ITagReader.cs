using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public interface ITagReader
{
    TrackTags Read(string filePath);
}
