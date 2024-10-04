using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using YoutubeChannelDownloader.Configurations;

namespace YoutubeChannelDownloader;

public class ChannelDownloaderService(Helper helper, IOptions<DownloadOptions> options, ILogger<ChannelDownloaderService> logger)
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly DownloadOptions _options = options.Value;

    public async Task DownloadVideosAsync(string channelId)
    {
        string dirPath = Path.Combine(_options.VideoFolderPath, channelId);
        string dataPath = Path.Combine(dirPath, "data.json");
        string videosPath = Path.Combine(dirPath, "videos");

        logger.LogDebug("Проверка наличия директории для канала: {ChannelId}", channelId);

        if (Directory.Exists(dirPath) == false)
        {
            logger.LogDebug("Директория не найдена. Создание директории: {DirPath}", dirPath);
            Directory.CreateDirectory(dirPath);
        }

        helper.RefreshDirectories(videosPath);
        List<VideoInfo>? videos;

        if (File.Exists(dataPath))
        {
            logger.LogDebug("Чтение данных видео из файла: {DataPath}", dataPath);
            string videoData = await File.ReadAllTextAsync(dataPath);
            videos = JsonSerializer.Deserialize<List<VideoInfo>>(videoData);

            if (videos != null && videos.Count != 0)
            {
                ValidateVideoStatuses(videos, videosPath);

                List<VideoInfo> videosToDownload = videos.Take(3)
                    .Where(info => info.Status is VideoStatus.NotDownloaded or VideoStatus.Error)
                    .ToList();

                await DownloadVideos(videosToDownload, videosPath);
            }
            else
            {
                logger.LogWarning("В файле data.json не найдено видео");
            }
        }
        else
        {
            logger.LogDebug("Файл data.json не найден. Начинаем загрузку видео для канала: {ChannelId}", channelId);
            videos = await helper.Download(channelId);
            videos.Reverse();
        }

        string updatedVideoData = JsonSerializer.Serialize(videos, _serializerOptions);
        await File.WriteAllTextAsync(dataPath, updatedVideoData, Encoding.UTF8);
        logger.LogDebug("Данные видео успешно обновлены и сохранены в файл: {DataPath}", dataPath);

        helper.RefreshDirectories(videosPath);
    }

    private async Task DownloadVideos(List<VideoInfo> videos, string videosPath)
    {
        logger.LogInformation("Найдено {DownloadableVideoCount} видео для загрузки", videos.Count);

        foreach (VideoInfo video in videos)
        {
            logger.LogDebug("Загрузка видео: {VideoTitle}", video.Title);
            video.Status = await helper.GetItem(video, videosPath);
        }
    }

    private void ValidateVideoStatuses(List<VideoInfo> videos, string videosPath)
    {
        foreach (VideoInfo video in videos)
        {
            if (video.Status != VideoStatus.Downloaded)
            {
                continue;
            }

            string videoFilePath = Path.Combine(videosPath, video.FileName);

            if (Directory.GetFiles(videosPath).Count(x => x.Contains(video.FileName, StringComparison.InvariantCultureIgnoreCase)) != 5)
            {
                logger.LogError("Видео {Title} имеет статус 'Загружено', но часть файлов не найдена: {FilePath}", video.Title, videoFilePath);
            }
            else
            {
                logger.LogTrace("Видео {Title} имеет статус 'Загружено' и файлы найдены: {FilePath}", video.Title, videoFilePath);
            }
        }
    }
}
