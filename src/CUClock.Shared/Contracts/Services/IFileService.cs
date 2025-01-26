namespace CUClock.Shared.Contracts.Services;

public interface IFileService
{
    T Read<T>(string fileName);

    T Read<T>(string folderPath, string fileName);

    void Save<T>(string fileName, T content);

    void Save<T>(string folderPath, string fileName, T content);

    void Delete(string folderPath, string fileName);
}
