using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using YoutubeDownloader.Api.Application;
using YoutubeDownloader.Api.Configurations;

namespace YoutubeDownloader.Api.Services;

public class DownloadService
{
    private readonly FFmpegConverter _converter;
    private readonly List<DownloadItem> _items = [];
    private readonly ILogger<DownloadService> _logger;
    private readonly DownloadOptions _options;
    private readonly YoutubeDownloadService _youtubeDownloadService;

    public DownloadService(
        ILogger<DownloadService> logger,
        YoutubeDownloadService youtubeDownloadService,
        FFmpegConverter converter,
        IOptions<DownloadOptions> options)
    {
        _logger = logger;
        _youtubeDownloadService = youtubeDownloadService;
        _converter = converter;
        _options = options.Value;

        RefreshDirectories();
    }

    public bool IsNeedDownloadAny => _items.Any(item => item.IsNeedDownloadAnyStream);

    public Operation<DownloadItem, string> FindItem(Guid id)
    {
        if (id == Guid.Empty)
        {
            return Operation.Error<string>("Id не может быть пустым");
        }

        DownloadItem? item = _items.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return item is not null
            ? Operation.Result(item)
            : Operation.Error<string>($"DownloadItem c id {id} не найден");
    }

    public async Task<DownloadItem> AddToQueueAsync(string url)
    {
        _logger.LogDebug("Попытка добавить в очередь: {Url}", url);

        Video video = await _youtubeDownloadService.GetVideoAsync(url);
        Guid id = Guid.NewGuid();

        StreamManifest streamManifest = await _youtubeDownloadService.GetStreamManifestAsync(url);

        Regex illegalInFileName = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);
        string videoTitle = illegalInFileName.Replace(video.Title, "_");

        List<DownloadItemSteam> streams = streamManifest.Streams.Select((stream, i) => new DownloadItemSteam
            {
                Id = i,
                TempName = $"{id}_{i}.{stream.Container.Name}",
                TempPath = Path.Combine(_options.TempFolderPath, $"{id}_{i}.{stream.Container.Name}"),
                FileName = $"{videoTitle}.{stream.Container.Name}",
                FilePath = Path.Combine(_options.FullVideoFolderPath, $"{videoTitle}.{stream.Container.Name}"),
                Stream = stream
            })
            .ToList();

        int streamId = streams.Count;

        IAudioStreamInfo highestAudioStream = (IAudioStreamInfo)streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

        Container[] videoTypes = [Container.WebM, Container.Mp4];

        foreach (Container videoType in videoTypes)
        {
            IVideoStreamInfo? highestVideoStream = streamManifest.GetVideoOnlyStreams()
                .Where(streamInfo => streamInfo.Container == videoType)
                .TryGetWithHighestVideoQuality();

            if (highestVideoStream is null)
            {
                continue;
            }

            streams.Insert(0, new DownloadItemSteam
            {
                Id = streamId,
                TempName = $"{id}_{streamId}.{videoType}",
                TempPath = Path.Combine(_options.TempFolderPath, $"{id}_{streamId}.{videoType}"),
                FileName = $"{videoTitle}.{videoType}",
                FilePath = Path.Combine(_options.FullVideoFolderPath, $"{videoTitle}.{videoType}"),
                AudioStreamInfo = highestAudioStream,
                VideoStreamInfo = highestVideoStream
            });

            streamId++;
        }

        DownloadItem item = DownloadItem.Create(id, url, streams, video).Result;

        _items.Add(item);
        _logger.LogDebug("Добавлено в очередь {Id}: {Url}", id, url);
        return item;
    }

    public void SetStreamToDownload(Guid downloadId, int streamId)
    {
        _logger.LogDebug("Попытка установить поток для скачивания: {Id} {StreamId}", downloadId, streamId);

        Operation<DownloadItem, string> itemOperation = FindItem(downloadId);

        if (itemOperation.Ok == false)
        {
            _logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", downloadId, itemOperation.Error);
            return;
        }

        DownloadItem item = itemOperation.Result;

        Operation<DownloadItemSteam, string> streamOperation = item.GetStream(streamId);

        if (streamOperation.Ok == false)
        {
            _logger.LogError("Не удалось получить поток: {StreamId} для элемента: {Id}, Ошибка: {Error}", streamId, downloadId, streamOperation.Error);
            return;
        }

        DownloadItemSteam stream = streamOperation.Result;
        stream.State = DownloadItemState.Wait;

        _logger.LogDebug("Успешно установлен поток для скачивания: {Id} {StreamId}", downloadId, streamId);
    }

    public async Task DownloadFromQueue()
    {
        DownloadItem? downloadItem = _items.FirstOrDefault(x => x.Streams.Any(itemSteam => itemSteam.State == DownloadItemState.Wait));

        if (downloadItem == null)
        {
            return;
        }

        DownloadItemSteam downloadStream = downloadItem.Streams.First(x => x.State == DownloadItemState.Wait);
        downloadStream.State = DownloadItemState.InProcess;
        _logger.LogDebug("Попытка скачать из очереди: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);

        CancellationTokenSource cancellationTokenSource = new();

        try
        {
            Task downloadTask = downloadStream.IsCombineAfterDownload
                ? DownloadCombinedStream(downloadStream, downloadItem, cancellationTokenSource.Token)
                : DownloadMuxedStream(downloadStream, cancellationTokenSource.Token);

            await downloadTask;
            downloadStream.State = DownloadItemState.Ready;
            _logger.LogDebug("Успешно скачан из очереди: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);
        }
        catch (Exception exception)
        {
            await cancellationTokenSource.CancelAsync();

            downloadStream.State = DownloadItemState.Error;
            _logger.LogError(exception, "Не полупилось скачать из очереди: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);
        }
    }

    private void RefreshDirectories()
    {
        string fullVideoFolderPath = _options.FullVideoFolderPath;
        string tempFolderPath = _options.TempFolderPath;

        try
        {
            if (Directory.Exists(fullVideoFolderPath) == false)
            {
                Directory.CreateDirectory(fullVideoFolderPath);
                _logger.LogInformation("Создана директория для видео: {FullVideoFolderPath}", fullVideoFolderPath);
            }

            FileInfo[] tempFiles = Directory.GetFiles(tempFolderPath)
                .Select(fileName => new FileInfo(fileName))
                .ToArray();

            double totalFileSize = tempFiles.Sum(fileInfo => fileInfo.Length) / 1024.0 / 1024;

            foreach (FileInfo file in tempFiles)
            {
                File.Delete(file.FullName);
                _logger.LogInformation("Удален временный файл: {File}", file);
            }

            _logger.LogInformation("Всего удалено временных файлов: {Count}, Объем: {TotalSize:F2} мегабайт", tempFiles.Length, totalFileSize);

            FileInfo[] mainFiles = Directory.GetFiles(fullVideoFolderPath)
                .Select(fileName => new FileInfo(fileName))
                .ToArray();

            double length = mainFiles.Sum(fileInfo => fileInfo.Length) / 1024.0 / 1024;

            _logger.LogInformation("Всего файлов в директории: {Count}, Объем: {TotalSize:F2} мегабайт", mainFiles.Length, length);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ошибка при обновлении директорий.");
        }
    }

    private async Task DownloadCombinedStream(DownloadItemSteam downloadStream, DownloadItem downloadItem, CancellationToken cancellationToken)
    {
        string audioPath = downloadStream.TempPath.AddSuffixToFileName("audio");
        string videoPath = downloadStream.TempPath.AddSuffixToFileName("video");

        if (downloadStream.AudioStreamInfo == null || downloadStream.VideoStreamInfo == null)
        {
            _logger.LogError("Не удалось объединить ({MethodName}). Нет видео или аудио", nameof(DownloadCombinedStream));
            return;
        }

        ValueTask audioTask = _youtubeDownloadService.DownloadWithProgressAsync(downloadStream.AudioStreamInfo,
            audioPath,
            downloadStream.Title,
            downloadItem.Video.Title,
            cancellationToken);

        ValueTask videoTask = _youtubeDownloadService.DownloadWithProgressAsync(downloadStream.VideoStreamInfo,
            videoPath,
            downloadStream.Title,
            downloadItem.Video.Title,
            cancellationToken);

        await Task.WhenAll(audioTask.AsTask(), videoTask.AsTask());

        _logger.LogDebug("Попытка объединить видео и аудио: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);

        double oldPercent = -1;

        Progress<double> progress = new(percent =>
        {
            if (percent - oldPercent < 0.02)
            {
                return;
            }

            _logger.LogDebug("Объединение: {Percent:P2}", percent);
            oldPercent = percent;
        });

        await _converter.ProcessAsync(downloadStream.FilePath, new[] { audioPath, videoPath }, progress, cancellationToken);

        _logger.LogDebug("Успешно объединено видео и аудио: {Id} {StreamId}", downloadItem.Id, downloadStream.Id);
    }

    private async Task DownloadMuxedStream(DownloadItemSteam downloadStream, CancellationToken cancellationToken)
    {
        await _youtubeDownloadService.DownloadWithProgressAsync(downloadStream, cancellationToken);

        try
        {
            File.Move(downloadStream.TempPath, downloadStream.FilePath, true);
            _logger.LogDebug("Файл успешно перемещен \nиз {TempPath} \nв  {FilePath}", downloadStream.TempPath, downloadStream.FilePath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Произошла ошибка при перемещении файла \nиз {TempPath} \nв  {FilePath}", downloadStream.TempPath, downloadStream.FilePath);
        }
    }
}