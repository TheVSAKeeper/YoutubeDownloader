﻿using Microsoft.Extensions.Logging;
using System.Globalization;
using YoutubeChannelDownloader.Extensions;
using YoutubeChannelDownloader.Models;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;

namespace YoutubeChannelDownloader.Services;

public class Helper(DownloadService downloadService, YoutubeService youtubeService, HttpClient httpClient, ILogger<Helper> logger)
{
    public async Task<List<VideoInfo>> Download(string channelUrl)
    {
        IAsyncEnumerable<VideoInfo> yVideos = GetUploadsInfoAsync(channelUrl);
        List<VideoInfo> videos = [];

        await foreach (VideoInfo? video in yVideos)
        {
            videos.Add(video);
            logger.LogDebug("Добавлено видео: {Title}", video.Title);
        }

        logger.LogInformation("Загружено {Count} видео из канала: {ChannelUrl}", videos.Count, channelUrl);
        return videos;
    }

    public async IAsyncEnumerable<VideoInfo> GetUploadsInfoAsync(string channelUrl)
    {
        IAsyncEnumerable<PlaylistVideo> videos = youtubeService.GetUploadsAsync(channelUrl);

        await foreach (PlaylistVideo video in videos)
        {
            string fileName = video.GetFileName();

            yield return new VideoInfo(video.Title,
                fileName,
                VideoState.NotDownloaded,
                video.Url,
                video.Thumbnails.TryGetWithHighestResolution()?.Url,
                video.PlaylistId);
        }
    }

    public async Task<VideoState> GetItem(VideoInfo videoInfo, string path)
    {
        string url = videoInfo.Url;

        logger.LogInformation("Начинаем загрузку видео: {VideoTitle} из {Url}", videoInfo.Title, url);
        (DownloadItem? item, DownloadItemStream? stream) = await downloadService.DownloadVideo(url, path);

        if (item == null)
        {
            logger.LogError("Ошибка при загрузке видео: {VideoTitle}", videoInfo.Title);
            return VideoState.Error;
        }

        await File.WriteAllTextAsync(Path.Combine(path, $"{videoInfo.FileName}_title.txt"), videoInfo.Title);
        await File.WriteAllTextAsync(Path.Combine(path, $"{videoInfo.FileName}_description.txt"), item.Video.Description);
        await File.WriteAllTextAsync(Path.Combine(path, $"{videoInfo.FileName}_upload-date.txt"), item.Video.UploadDate.ToString(CultureInfo.InvariantCulture));
        await DownloadThumbnail(videoInfo.ThumbnailUrl, Path.Combine(path, $"{videoInfo.FileName}_thumbnail.jpg"));

        logger.LogInformation("Загрузка видео завершена: {VideoTitle}", videoInfo.Title);

        return VideoState.Downloaded;
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
                logger.LogInformation("Создана временная директория для видео: {FullTempFolderPath}", tempFolderPath);
            }

            FileInfo[] tempFiles = Directory.GetFiles(tempFolderPath)
                .Select(fileName => new FileInfo(fileName))
                .ToArray();

            double totalFileSize = tempFiles.Sum(fileInfo => fileInfo.Length) / 1024.0 / 1024;

            foreach (FileInfo file in tempFiles)
            {
                File.Delete(file.FullName);
                logger.LogDebug("Удален временный файл: {File}", file.FullName);
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
            logger.LogError(exception, "Ошибка при обновлении директорий: {Path}", path);
        }
    }

    private async Task DownloadThumbnail(string? thumbnailUrl, string savePath)
    {
        if (string.IsNullOrEmpty(thumbnailUrl))
        {
            logger.LogWarning("URL миниатюры пустой или недоступен. Пропуск загрузки миниатюры");
            return;
        }

        try
        {
            logger.LogDebug("Начинаем загрузку миниатюры: {ThumbnailUrl}", thumbnailUrl);
            HttpResponseMessage response = await httpClient.GetAsync(thumbnailUrl);
            response.EnsureSuccessStatusCode();

            await using FileStream fileStream = new(savePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);

            logger.LogDebug("Миниатюра успешно сохранена в: {Path}", savePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при загрузке миниатюры: {ThumbnailUrl}", thumbnailUrl);
        }
    }
}
