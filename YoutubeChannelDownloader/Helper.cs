using Microsoft.Extensions.Logging;
using YoutubeChannelDownloader.Extensions;
using YoutubeChannelDownloader.Models;
using YoutubeExplode;
using YoutubeExplode.Playlists;

namespace YoutubeChannelDownloader;

public class Helper(DownloadService downloadService, HttpClient httpClient, ILogger<Helper> logger)
{
    public async Task<List<VideoInfo>> Download(string chanelUrl)
    {
        YoutubeClient youtube = new();
        IAsyncEnumerable<PlaylistVideo> yVideos = youtube.Channels.GetUploadsAsync(chanelUrl);

        List<VideoInfo> videos = [];

        await foreach (PlaylistVideo item in yVideos)
        {
            string fileName = item.GetVideoFileName();

            VideoInfo video = new(item.Title,
                fileName,
                0, item.Url,
                item.Thumbnails.OrderByDescending(x => x.Resolution.Area).FirstOrDefault()?.Url,
                item.PlaylistId.Value);

            videos.Add(video);
            logger.LogInformation("Add video: {Title}", item.Title);
        }

        return videos;
    }

    public async Task GetItem(VideoInfo videoInfo, string path)
    {
        string url = videoInfo.Url;

        (DownloadItem? item, DownloadItemStream? stream) = await downloadService.DownloadVideo(url, path);

        string? thumbnailUrl = videoInfo.ThumbnailUrl;

        if (string.IsNullOrEmpty(thumbnailUrl) == false)
        {
            await DownloadThumbnail(thumbnailUrl, Path.Combine(path, videoInfo.FileName + "_thumbnail.jpg"));
        }

        await File.WriteAllTextAsync(Path.Combine(path, videoInfo.FileName + "_title.txt"), videoInfo.Title);
        await File.WriteAllTextAsync(Path.Combine(path, videoInfo.FileName + "_description.txt"), item.Video.Description);
        await File.WriteAllTextAsync(Path.Combine(path, videoInfo.FileName + "_upload-date.txt"), item.Video.UploadDate.DateTime.ToLongTimeString());
    }

    public void RefreshDirectories(string path)
    {
        string tempFolderPath = Path.Combine(path, ".temp");

        try
        {
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
                logger.LogInformation("Создана директория для видео: {FullVideoFolderPath}", path);
            }

            if (Directory.Exists(tempFolderPath) == false)
            {
                Directory.CreateDirectory(tempFolderPath);
                logger.LogInformation("Создана временная директория для видео: {FullVideoFolderPath}", path);
            }

            FileInfo[] tempFiles = Directory.GetFiles(tempFolderPath)
                .Select(fileName => new FileInfo(fileName))
                .ToArray();

            double totalFileSize = tempFiles.Sum(fileInfo => fileInfo.Length) / 1024.0 / 1024;

            foreach (FileInfo file in tempFiles)
            {
                File.Delete(file.FullName);
                logger.LogInformation("Удален временный файл: {File}", file);
            }

            logger.LogInformation("Всего удалено временных файлов: {Count}, Объем: {TotalSize:F2} мегабайт", tempFiles.Length, totalFileSize);

            FileInfo[] mainFiles = Directory.GetFiles(path)
                .Select(fileName => new FileInfo(fileName))
                .ToArray();

            double length = mainFiles.Sum(fileInfo => fileInfo.Length) / 1024.0 / 1024;

            logger.LogInformation("Всего файлов в директории: {Count}, Объем: {TotalSize:F2} мегабайт", mainFiles.Length, length);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Ошибка при обновлении директорий");
        }
    }

    private async Task DownloadThumbnail(string thumbnailUrl, string savePath)
    {

        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(thumbnailUrl);
            response.EnsureSuccessStatusCode();

            await using FileStream fileStream = new(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            logger.LogInformation("Миниатюра сохранена в: {Path}", savePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке миниатюры: {ThumbnailUrl}", thumbnailUrl);
        }
    }
}
