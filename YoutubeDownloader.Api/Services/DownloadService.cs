using Microsoft.Extensions.Options;
using YoutubeDownloader.Api.Application.Extensions;
using YoutubeDownloader.Api.Configurations;

namespace YoutubeDownloader.Api.Services;

public class DownloadService
{
    private readonly DownloadOptions _options;
    private readonly FFmpegConverter _converter;
    private readonly ILogger<DownloadService> _logger;
    private readonly List<DownloadItem> _items = [];
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

    public Operation<DownloadItem, string> FindItem(string id)
    {
        DownloadItem? item = _items.FirstOrDefault(downloadItem => downloadItem.Id == id);

        return item is not null
            ? Operation.Result(item)
            : Operation.Error<string>($"DownloadItem c id {id} не найден");
    }

    public async Task<DownloadItem> AddToQueueAsync(string url)
    {
        _logger.LogDebug("Попытка добавить в очередь: {Url}", url);

        DownloadItem? downloadItem = _items.FirstOrDefault(downloadItem => downloadItem.Url == url);

        if (downloadItem is not null)
        {
            _logger.LogDebug("Уже существует в очереди {Id}: {Url}", downloadItem.Id, url);
            return downloadItem;
        }

        Video video = await _youtubeDownloadService.GetVideoAsync(url);

        StreamManifest streamManifest = await _youtubeDownloadService.GetStreamManifestAsync(url);

        List<DownloadItemStream> streams = streamManifest.Streams
            .Select((stream, i) => DownloadItemStream.Create(i, _options.TempFolderPath, _options.FullVideoFolderPath, video, stream))
            .GetSuccessfulResults()
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

            Operation<DownloadItemStream> itemOperation = DownloadItemStream.Create(streamId,
                _options.TempFolderPath,
                _options.FullVideoFolderPath,
                video,
                highestAudioStream,
                highestVideoStream);

            if (itemOperation.Ok == false)
            {
                continue;
            }

            streams.Insert(0, itemOperation.Result);
            streamId++;
        }

        DownloadItem item = DownloadItem.Create(url, streams, video).Result;

        _items.Add(item);
        _logger.LogDebug("Добавлено в очередь {Id}: {Url}", item.Id, url);
        return item;
    }

    public void SetStreamToDownload(string downloadId, int streamId)
    {
        _logger.LogDebug("Попытка установить поток для скачивания: {Id} {StreamId}", downloadId, streamId);

        Operation<DownloadItem, string> itemOperation = FindItem(downloadId);

        if (itemOperation.Ok == false)
        {
            _logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", downloadId, itemOperation.Error);
            return;
        }

        DownloadItem item = itemOperation.Result;

        Operation<DownloadItemStream, string> streamOperation = item.GetStream(streamId);

        if (streamOperation.Ok == false)
        {
            _logger.LogError("Не удалось получить поток: {StreamId} для элемента: {Id}, Ошибка: {Error}", streamId, downloadId, streamOperation.Error);
            return;
        }

        DownloadItemStream stream = streamOperation.Result;
        stream.State = DownloadItemState.Wait;

        _logger.LogDebug("Успешно установлен поток для скачивания: {Id} {StreamId}", downloadId, streamId);
    }

    public async Task DownloadFromQueue()
    {
        DownloadItem? downloadItem = _items.FirstOrDefault(x => x.Streams.Any(itemSteam => itemSteam.State == DownloadItemState.Wait));
        DownloadItemStream? downloadStream = downloadItem?.GetWaitStreams().FirstOrDefault();

        if (downloadStream is null || downloadItem is null)
        {
            _logger.LogWarning("Очередь скачивания пустая");
            return;
        }

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

    private async Task DownloadCombinedStream(DownloadItemStream downloadStream, DownloadItem downloadItem, CancellationToken cancellationToken)
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

    private async Task DownloadMuxedStream(DownloadItemStream downloadStream, CancellationToken cancellationToken)
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
