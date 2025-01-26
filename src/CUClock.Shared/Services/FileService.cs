using System.Text;

using CUClock.Shared.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;

namespace CUClock.Shared.Services;

public class FileService(ILogger<FileService> logger) : IFileService
{
    public T Read<T>(string fileName)
    {
        var folderPath = FileSystem.Current.AppDataDirectory;
        logger.LogInformation("Reading file '{fileName}' from '{folderPath}'", fileName, folderPath);
        return Read<T>(folderPath, fileName);
    }

    public T Read<T>(string folderPath, string fileName)
    {
        if (folderPath.Length == 0)
        {
            folderPath = FileSystem.Current.AppDataDirectory;
        }
        logger.LogInformation("Reading file '{fileName}' from '{folderPath}'", fileName, folderPath);
        logger.LogInformation("Current directory: {dir}", Directory.GetCurrentDirectory());

        var path = Path.Combine(folderPath, fileName);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        return default;
    }

    public void Save<T>(string fileName, T content)
    {
        var folderPath = FileSystem.Current.AppDataDirectory;
        logger.LogInformation("Saving file '{fileName}' to '{folderPath}'", fileName, folderPath);
        Save(folderPath, fileName, content);
    }

    public void Save<T>(string folderPath, string fileName, T content)
    {
        if (folderPath.Length == 0)
        {
            folderPath = FileSystem.Current.AppDataDirectory;
        }
        logger.LogInformation("Saving file '{fileName}' to '{folderPath}'", fileName, folderPath);
        logger.LogInformation("Current directory: {dir}", Directory.GetCurrentDirectory());

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileContent = JsonConvert.SerializeObject(content);
        File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
    }

    public void Delete(string folderPath, string fileName)
    {
        if (fileName != null && File.Exists(Path.Combine(folderPath, fileName)))
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}
