namespace YoutubeDownloader.Api.Models.Requests;

/// <summary>
///     Запрос на добавление URL-адреса в очередь или процесс загрузки.
/// </summary>
/// <param name="Url">URL-адрес, который нужно добавить в очередь.</param>
public record AddToDownloadRequest(string Url);