namespace YoutubeDownloader.Api.Application.Extensions;

public static class FilePathExtensions
{
    public static string AddPrefixToFileName(this string filePath, string prefix)
    {
        string fileName = $"{prefix}_{Path.GetFileName(filePath)}";
        string directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException();
        return Path.Combine(directoryPath, fileName);
    }

    public static string AddSuffixToFileName(this string filePath, string suffix)
    {
        string fileName = $"{Path.GetFileNameWithoutExtension(filePath)}_{suffix}{Path.GetExtension(filePath)}";
        string directoryPath = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException();
        return Path.Combine(directoryPath, fileName);
    }
}