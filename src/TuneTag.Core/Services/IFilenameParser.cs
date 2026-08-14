using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public interface IFilenameParser
{
    FilenameTagSuggestion Parse(string fileNameWithoutExtension);
}
