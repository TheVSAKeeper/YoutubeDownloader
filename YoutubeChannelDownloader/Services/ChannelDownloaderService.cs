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

public class ChannelDownloaderService(Helper helper, IOptions<DownloadOptions> options, ILogger<ChannelDownloaderService> logger)
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly DownloadOptions _options = options.Value;
    private List<VideoInfo> _videos = [];

    public async Task DownloadPlaylists(string channelId)
    {
        string dirPath = Path.Combine(_options.VideoFolderPath, channelId);
        string dataPath = Path.Combine(dirPath, "playlists.json");
        string videosPath = Path.Combine(dirPath, "playlists");

        logger.LogDebug("Проверка наличия директории для канала: {ChannelId}", channelId);

        if (Directory.Exists(dirPath) == false)
        {
            logger.LogDebug("Директория не найдена. Создание директории: {DirPath}", dirPath);
            Directory.CreateDirectory(dirPath);
        }

        if (Directory.Exists(videosPath) == false)
        {
            Directory.CreateDirectory(videosPath);
            logger.LogInformation("Создана директория для видео: {FullVideoFolderPath}", videosPath);
        }

        logger.LogDebug("Файл data.json не найден. Начинаем загрузку видео для канала: {ChannelId}", channelId);
        List<PlaylistInfo> playlists = await helper.DownloadPlaylist(_videos);

        foreach (PlaylistInfo playlist in playlists)
        {
            string path = Path.Combine(videosPath, playlist.Id);
            Directory.CreateDirectory(path);
            await helper.GetPlaylist(playlist, path);
        }

        string updatedVideoData = JsonSerializer.Serialize(playlists, _serializerOptions);
        await File.WriteAllTextAsync(dataPath, updatedVideoData, Encoding.UTF8);
        logger.LogDebug("Данные видео успешно обновлены и сохранены в файл: {DataPath}", dataPath);
    }

    public async Task DownloadVideosAsync(string channelUrl)
    {
        Channel? channel = await helper.GetChannel(channelUrl);

        if (channel == null)
        {
            logger.LogError("Не удалось найти канал по ссылке: {Url}", channelUrl);
            return;
        }

        string channelTitle = channel.Title.GetFileName();
        string dirPath = Path.Combine(_options.VideoFolderPath, channelTitle);
        string dataPath = Path.Combine(dirPath, "data.json");
        string videosPath = Path.Combine(dirPath, "videos");

        logger.LogDebug("Проверка наличия директории для канала: {Channel}", channelTitle);

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

                List<VideoInfo> videosToDownload = videos
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
            logger.LogDebug("Файл data.json не найден. Начинаем загрузку информации о видео для канала: {Channel}", channelTitle);
            videos = await helper.Download(channel.Id);
            videos.Reverse();
        }

        _videos = [.. videos!];

        string updatedVideoData = JsonSerializer.Serialize(videos, _serializerOptions);
        await File.WriteAllTextAsync(dataPath, updatedVideoData, Encoding.UTF8);
        logger.LogDebug("Данные видео успешно обновлены и сохранены в файл: {DataPath}", dataPath);

        helper.RefreshDirectories(videosPath);
    }

    private async Task DownloadVideos(List<VideoInfo> videos, string videosPath)
    {
        logger.LogInformation("Найдено {DownloadableVideoCount} видео для загрузки", videos.Count);

        var errorCount = 0;
        foreach (VideoInfo video in videos)
        {
            logger.LogDebug("Загрузка видео: {VideoTitle}", video.Title);

            try
            {
                video.Status = await helper.GetItem(video, videosPath);
                errorCount = 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "упс: " + ex.Message);
                Thread.Sleep(5000 * errorCount);
                errorCount++;
            }
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

            var count1 = Directory.GetFiles(videosPath).Count(x => x.Contains(video.FileName + ".", StringComparison.InvariantCultureIgnoreCase));
            var count2 = Directory.GetFiles(videosPath).Count(x => x.Contains(video.FileName + "_", StringComparison.InvariantCultureIgnoreCase));
            if (count1 != 1 && count2 != 4)
            {
                logger.LogDebug("main count expected 1: {count1}, secondary count expected 4: {count2}", count1, count2);
                logger.LogError("Видео {Title} имеет статус 'Загружено', но часть файлов не найдена: {FilePath}", video.Title, videoFilePath);
                video.Status = VideoStatus.NotDownloaded;
            }
            else
            {
                logger.LogTrace("Видео {Title} имеет статус 'Загружено' и файлы найдены: {FilePath}", video.Title, videoFilePath);
            }
        }
    }
}
