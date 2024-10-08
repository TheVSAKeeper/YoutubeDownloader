using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using YoutubeChannelDownloader.Configurations;
using YoutubeChannelDownloader.Extensions;
using YoutubeChannelDownloader.Models;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;

namespace YoutubeChannelDownloader.Services;

public class ChannelService(YoutubeService youtubeService, Helper helper, IOptions<DownloadOptions> options, ILogger<ChannelService> logger)
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly DownloadOptions _options = options.Value;

    public async Task DownloadVideosAsync(string channelUrl)
    {
        Channel? channel = await youtubeService.GetChannel(channelUrl);

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
                VideoInfo? last = videos.LastOrDefault();

                if (last != null)
                {
                    IAsyncEnumerable<PlaylistVideo> uploads = youtubeService.GetUploadsAsync(channel.Id);

                    List<VideoInfo> newVideos = [];

                    await foreach (PlaylistVideo upload in uploads)
                    {
                        if (last.Url == upload.Url)
                        {
                            break;
                        }

                        string fileName = upload.GetFileName();

                        VideoInfo video = new(upload.Title,
                            fileName,
                            VideoState.NotDownloaded,
                            upload.Url,
                            upload.Thumbnails.TryGetWithHighestResolution()?.Url,
                            upload.PlaylistId);

                        newVideos.Add(video);
                        logger.LogInformation("Добавлено новое видео: {Title}", upload.Title);
                    }

                    newVideos.Reverse();
                    videos.AddRange(newVideos);
                }

                ValidateVideoStatuses(videos, videosPath);

                List<VideoInfo> videosToDownload = videos
                    .Where(info => info.State is VideoState.NotDownloaded or VideoState.Error)
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

        string updatedVideoData = JsonSerializer.Serialize(videos, _serializerOptions);
        await File.WriteAllTextAsync(dataPath, updatedVideoData, Encoding.UTF8);
        logger.LogDebug("Данные видео успешно обновлены и сохранены в файл: {DataPath}", dataPath);

        helper.RefreshDirectories(videosPath);
    }

    private async Task DownloadVideos(List<VideoInfo> videos, string videosPath)
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
                logger.LogError(ex, "упс: {Message}", ex.Message);
                Thread.Sleep(5000 * errorCount);
                errorCount++;
            }
        }
    }

    private void ValidateVideoStatuses(List<VideoInfo> videos, string videosPath)
    {
        foreach (VideoInfo video in videos)
        {
            if (video.State != VideoState.Downloaded)
            {
                continue;
            }

            string videoFilePath = Path.Combine(videosPath, video.FileName);

            int count1 = Directory.GetFiles(videosPath).Count(x => x.Contains(video.FileName + ".", StringComparison.InvariantCultureIgnoreCase));
            int count2 = Directory.GetFiles(videosPath).Count(x => x.Contains(video.FileName + "_", StringComparison.InvariantCultureIgnoreCase));

            if (count1 != 1 && count2 != 4)
            {
                logger.LogDebug("main count expected 1: {Count1}, secondary count expected 4: {Count2}", count1, count2);
                logger.LogError("Видео {Title} имеет статус 'Загружено', но часть файлов не найдена: {FilePath}", video.Title, videoFilePath);
                video.State = VideoState.Error;
            }
            else
            {
                logger.LogTrace("Видео {Title} имеет статус 'Загружено' и файлы найдены: {FilePath}", video.Title, videoFilePath);
            }
        }
    }
}
