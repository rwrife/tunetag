using TuneTag.Core.Models;

namespace TuneTag.Core.Services;

public interface IArtService
{
    AlbumArt? ReadPrimary(string filePath);

    void SetPrimary(string filePath, AlbumArt art);

    void Remove(string filePath);

    string ExtractPrimary(string filePath, string outputPath);

    int ApplyPrimaryToFolder(string folderPath, AlbumArt art, IEnumerable<string>? supportedExtensions = null);
}
