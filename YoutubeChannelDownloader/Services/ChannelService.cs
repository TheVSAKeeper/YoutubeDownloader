using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using YoutubeChannelDownloader.Configurations;
using YoutubeChannelDownloader.Extensions;
using YoutubeChannelDownloader.Models;
using YoutubeExplode.Channels;

namespace YoutubeChannelDownloader.Services;

public class ChannelService(YoutubeService youtubeService, Helper helper, IOptions<DownloadOptions> options, ILogger<ChannelService> logger)
{
    private readonly DownloadOptions _options = options.Value;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    ///     Основной метод для загрузки видео с канала YouTube по его URL.
    /// </summary>
    /// <param name="channelUrl">URL канала YouTube.</param>
    public async Task DownloadVideosAsync(string channelUrl)
    {
        Channel? channel = await youtubeService.GetChannel(channelUrl);

        if (channel == null)
        {
            logger.LogError("Не удалось найти канал по ссылке: {Url}", channelUrl);
            return;
        }

        string channelTitle = channel.Title.GetFileName();
        string channelPath = Path.Combine(_options.VideoFolderPath, channelTitle);
        string dataPath = Path.Combine(channelPath, "data.json");
        string videosPath = Path.Combine(channelPath, "videos");

        EnsureDirectoriesExists(channelPath, videosPath);

        List<VideoInfo>? videos = await LoadVideosData(channel.Id, channelTitle, dataPath);

        if (videos == null)
        {
            return;
        }

        await DownloadVideos(videos, dataPath, videosPath);
    }

    /// <summary>
    ///     Проверяет наличие директории для канала и создает её, если необходимо.
    /// </summary>
    /// <param name="channelPath">Путь к директории канала.</param>
    /// <param name="videosPath">Путь к директории для хранения видео.</param>
    private void EnsureDirectoriesExists(string channelPath, string videosPath)
    {
        logger.LogDebug("Проверка наличия директории для канала");

        if (Directory.Exists(channelPath) == false)
        {
            logger.LogDebug("Создание директории: {ChannelPath}", channelPath);
            Directory.CreateDirectory(channelPath);
        }

        helper.RefreshDirectories(videosPath);
    }

    /// <summary>
    ///     Получает существующие видео из файла данных или загружает новые данные о видео с канала.
    /// </summary>
    /// <param name="channelId">ID канала.</param>
    /// <param name="channelTitle">Название канала.</param>
    /// <param name="dataPath">Путь к файлу data.json.</param>
    /// <returns>Список видео или null, если данные не найдены.</returns>
    private async Task<List<VideoInfo>?> LoadVideosData(ChannelId channelId, string channelTitle, string dataPath)
    {
        if (File.Exists(dataPath))
        {
            logger.LogDebug("Чтение данных видео из файла: {DataPath}", dataPath);
            string videoData = await File.ReadAllTextAsync(dataPath);
            List<VideoInfo>? savedVideos = JsonSerializer.Deserialize<List<VideoInfo>>(videoData);

            if (savedVideos is { Count: > 0 })
            {
                await UpdateVideosData(savedVideos, channelId);
                return savedVideos;
            }

            logger.LogWarning("Файл data.json не содержит информации о видео");
        }
        else
        {
            logger.LogDebug("Файл data.json не найден");
        }

        logger.LogDebug("Загрузка информации о загрузках на канале: {Channel}", channelTitle);
        List<VideoInfo> newVideos = await helper.Download(channelId);
        newVideos.Reverse();

        return newVideos;
    }

    /// <summary>
    ///     Обновляет список видео, добавляя новые загрузки, если они есть.
    /// </summary>
    /// <param name="videos">Список видео для обновления.</param>
    /// <param name="channelId">ID канала.</param>
    private async Task UpdateVideosData(List<VideoInfo> videos, ChannelId channelId)
    {
        VideoInfo? lastVideo = videos.LastOrDefault();

        if (lastVideo != null)
        {
            List<VideoInfo> newVideos = await FetchNewVideoUploads(channelId, lastVideo.Url);

            if (newVideos.Count > 0)
            {
                videos.AddRange(newVideos);
                logger.LogInformation("Найдено {Count} новых видео", newVideos.Count);
            }
        }
    }

    /// <summary>
    ///     Обрабатывает и загружает новые видео, обновляет файл данных с информацией о видео.
    /// </summary>
    /// <param name="videos">Список видео.</param>
    /// <param name="dataPath">Путь к файлу данных.</param>
    /// <param name="videosPath">Путь к директории с видеофайлами.</param>
    private async Task DownloadVideos(List<VideoInfo> videos, string dataPath, string videosPath)
    {
        ValidateVideoState(videos, videosPath);

        List<VideoInfo> videosToDownload = videos.Where(info => info.State is VideoState.NotDownloaded or VideoState.Error).ToList();
        await DownloadPendingVideos(videosToDownload, videosPath);

        string updatedVideoData = JsonSerializer.Serialize(videos, _serializerOptions);
        await File.WriteAllTextAsync(dataPath, updatedVideoData, Encoding.UTF8);
        logger.LogDebug("Данные видео обновлены и сохранены в файл: {DataPath}", dataPath);
    }

    /// <summary>
    ///     Получает новые загруженные видео с канала, которые не были скачаны.
    /// </summary>
    /// <param name="channelId">ID канала.</param>
    /// <param name="lastVideoUrl">URL последнего загруженного видео.</param>
    /// <returns>Список новых видео.</returns>
    private async Task<List<VideoInfo>> FetchNewVideoUploads(ChannelId channelId, string lastVideoUrl)
    {
        List<VideoInfo> newVideos = [];
        IAsyncEnumerable<VideoInfo> uploads = helper.GetUploadsInfoAsync(channelId);

        await foreach (VideoInfo upload in uploads)
        {
            if (lastVideoUrl == upload.Url)
            {
                break;
            }

            newVideos.Add(upload);
            logger.LogDebug("Добавлено новое видео: {Title}", upload.Title);
        }

        newVideos.Reverse();
        return newVideos;
    }

    /// <summary>
    ///     Загружает видео, которые ещё не были скачаны.
    /// </summary>
    /// <param name="videos">Список видео для загрузки.</param>
    /// <param name="videosPath">Путь к директории для хранения видео.</param>
    private async Task DownloadPendingVideos(List<VideoInfo> videos, string videosPath)
    {
        logger.LogInformation("Найдено {DownloadableVideoCount} видео для загрузки", videos.Count);

        int errorCount = 0;

        foreach (VideoInfo video in videos)
        {
            logger.LogDebug("Загрузка видео: {VideoTitle}", video.Title);

            try
            {
                video.State = await helper.GetItem(video, videosPath);
                errorCount = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка: {Message}", ex.Message);
                Thread.Sleep(5000 * errorCount);
                errorCount++;
            }
        }
    }

    /// <summary>
    ///     Проверяет, все ли файлы загруженных видео присутствуют в директории, и обновляет статус видео.
    /// </summary>
    /// <param name="videos">Список видео.</param>
    /// <param name="videosPath">Путь к директории с видео.</param>
    private void ValidateVideoState(List<VideoInfo> videos, string videosPath)
    {
        foreach (VideoInfo video in videos)
        {
            if (video.State != VideoState.Downloaded)
            {
                continue;
            }

            string videoFilePath = Path.Combine(videosPath, video.FileName);
            string[] allFiles = Directory.GetFiles(videosPath);

            int mainFileCount = allFiles.Count(x => x.Contains($"{video.FileName}.", StringComparison.InvariantCultureIgnoreCase));
            int infoFileCount = allFiles.Count(x => x.Contains($"{video.FileName}_", StringComparison.InvariantCultureIgnoreCase));

            int neededMainCount = 1;
            int neededInfoCount = 4;

            if (mainFileCount != neededMainCount || infoFileCount != neededInfoCount)
            {
                logger.LogDebug("Ожидаемое количество основных файлов: {NeededMainCount}, Найдено: {FoundMainCount}", neededMainCount, mainFileCount);
                logger.LogDebug("Ожидаемое количество информационных файлов: {NeededInfoCount}, Найдено: {FoundInfoCount}", neededInfoCount, infoFileCount);
                logger.LogError("Видео '{Title}' имеет статус 'Загружено', но не найдены необходимые файлы: {FilePath}", video.Title, videoFilePath);
                video.State = VideoState.Error;
            }
            else
            {
                logger.LogTrace("Видео '{Title}' имеет статус 'Загружено' и все файлы найдены: {FilePath}", video.Title, videoFilePath);
            }
        }
    }
}
