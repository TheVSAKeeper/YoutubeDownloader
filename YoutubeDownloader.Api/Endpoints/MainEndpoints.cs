using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Api.Application.Extensions;
using YoutubeDownloader.Api.Models.Requests;
using YoutubeDownloader.Api.Services;

namespace YoutubeDownloader.Api.Endpoints;

public sealed class MainEndpointsEndpoints : AppDefinition
{
    public override void ConfigureApplication(WebApplication app)
    {
        app.MapMainEndpointsEndpoints();
    }
}

internal static class MainEndpointsEndpointsExtensions
{
    public static void MapMainEndpointsEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/main/").WithTags("Main");

        group.MapPost("add-to-download", AddToDownload)
            .WithName("AddToDownload")
            .WithSummary("Добавить новый элемент в очередь загрузки")
            .WithDescription("Позволяет добавить новый элемент в очередь загрузки. Элемент идентифицируется по его уникальному ID.")
            .WithOpenApi();

        group.MapPost("add-stream-to-download", AddStreamToDownload)
            .WithName("AddStreamToDownload")
            .WithSummary("Добавить поток к элементу загрузки")
            .WithDescription("Позволяет добавить новый поток к элементу загрузки. Элемент и поток идентифицируются по их уникальным ID.")
            .WithMetadata()
            .WithOpenApi();

        group.MapGet("download", Download)
            .WithName("Download")
            .WithSummary("Скачать поток элемента загрузки")
            .WithDescription("Позволяет скачать конкретный поток элемента загрузки. Элемент и поток идентифицируются по их уникальным ID.")
            .WithOpenApi();

        group.MapGet("state/{id}", GetDownloadItemState)
            .WithName("GetDownloadItemState")
            .WithSummary("Получить состояние элемента загрузки")
            .WithDescription("Возвращает текущее состояние элемента загрузки. Элемент идентифицируется по его уникальному ID.")
            .WithOpenApi();

        group.MapGet("download-item/{id}/video", GetDownloadItemVideo)
            .WithName("GetDownloadItemVideo")
            .WithSummary("Получить видео из элемента загрузки")
            .WithDescription("Возвращает видео элемента загрузки. Элемент идентифицируется по его уникальному ID.")
            .WithOpenApi();
    }

    private static async Task<Results<Ok<StateModel>, BadRequest<string>>> AddToDownload([FromBody] AddToDownloadRequest request, DownloadService downloadService, ILogger<Endpoint> logger)
    {
        try
        {
            logger.LogInformation("Добавление в очередь загрузки: {Url}", request.Url);
            DownloadItem item = await downloadService.AddToQueueAsync(request.Url);
            Operation<StateModel> operation = StateModel.Create(item);

            if (operation.Ok)
            {
                logger.LogInformation("Успешно добавлено в очередь загрузки: {Id}", item.Id);
                return TypedResults.Ok(operation.Result);
            }

            logger.LogError("Не удалось получить состояние для элемента: {Id}", item.Id);
            return TypedResults.BadRequest("Не удалось получить состояние");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Произошла ошибка при добавлении в очередь загрузки");
            return TypedResults.BadRequest(exception.Message);
        }
    }

    private static Results<Ok<string>, BadRequest<string>> AddStreamToDownload([FromBody] AddStreamToDownloadRequest request, DownloadService downloadService, ILogger<Endpoint> logger)
    {
        try
        {
            logger.LogInformation("Добавление потока: {StreamId} в очередь загрузки для элемента: {Id}", request.StreamId, request.Id);
            downloadService.SetStreamToDownload(request.Id, request.StreamId);

            logger.LogInformation("Успешно добавлен поток: {StreamId} в очередь загрузки для элемента: {Id}", request.StreamId, request.Id);
            return TypedResults.Ok("Всё оки");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Произошла ошибка при добавлении потока: {StreamId} в очередь загрузки для элемента: {Id}", request.StreamId, request.Id);
            return TypedResults.BadRequest(exception.Message);
        }
    }

    private static Results<FileStreamHttpResult, BadRequest<string>> Download([FromBody] AddStreamToDownloadRequest request, DownloadService downloadService, ILogger<Endpoint> logger)
    {
        logger.LogInformation("Скачивание потока: {StreamId} для элемента: {Id}", request.StreamId, request.Id);
        Operation<DownloadItem, string> itemOperation = downloadService.FindItem(request.Id);

        if (itemOperation.Ok == false)
        {
            logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", request.Id, itemOperation.Error);
            return itemOperation.ToResponse();
        }

        DownloadItem item = itemOperation.Result;

        Operation<DownloadItemStream, string> streamOperation = item.GetStream(request.StreamId);

        if (streamOperation.Ok == false)
        {
            logger.LogError("Не удалось получить поток: {StreamId} для элемента: {Id}, Ошибка: {Error}", request.StreamId, request.Id, streamOperation.Error);
            return streamOperation.ToResponse();
        }

        DownloadItemStream stream = streamOperation.Result;

        if (stream.State != DownloadItemState.Ready)
        {
            logger.LogError("Поток: {StreamId} для элемента: {Id} не готов, Текущее состояние: {State}", request.StreamId, request.Id, stream.State);
            return TypedResults.BadRequest($"Состояние не готово. Текущее {stream.State}");
        }

        string type = stream.VideoType;
        FileStream fileStream = new(stream.FilePath, FileMode.Open, FileAccess.Read);

        logger.LogInformation("Успешно скачан поток: {StreamId} для элемента: {Id}", request.StreamId, request.Id);
        return TypedResults.Stream(fileStream, $"video/{type}", stream.FileName, enableRangeProcessing: true);
    }

    private static Results<Ok<StateModel>, BadRequest<string>> GetDownloadItemState(string id, DownloadService downloadService, ILogger<Endpoint> logger)
    {
        logger.LogInformation("Получение состояния элемента загрузки: {Id}", id);
        Operation<DownloadItem, string> itemOperation = downloadService.FindItem(id);

        if (itemOperation.Ok == false)
        {
            logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", id, itemOperation.Error);
            return itemOperation.ToResponse();
        }

        DownloadItem item = itemOperation.Result;

        Operation<StateModel> stateModelOperation = StateModel.Create(item);

        if (stateModelOperation.Ok)
        {
            logger.LogInformation("Успешно получено состояние элемента загрузки: {Id}", id);
            return TypedResults.Ok(stateModelOperation.Result);
        }

        logger.LogError("Не удалось получить состояние модели для элемента: {Id}", id);
        return TypedResults.BadRequest("Не удалось получить состояние");
    }

    private static Results<Ok<Video>, BadRequest<string>> GetDownloadItemVideo(string id, DownloadService downloadService, ILogger<Endpoint> logger)
    {
        Operation<DownloadItem, string> itemOperation = downloadService.FindItem(id);

        if (itemOperation.Ok)
        {
            return TypedResults.Ok(itemOperation.Result.Video);
        }

        logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", id, itemOperation.Error);
        return TypedResults.BadRequest(itemOperation.Error);
    }
}
