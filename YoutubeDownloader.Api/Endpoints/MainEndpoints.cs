using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Api.Models;
using YoutubeDownloader.Api.Models.Requests;
using YoutubeDownloader.Api.Services;

namespace YoutubeDownloader.Api.Endpoints;

public sealed class MainEndpointsEndpoints : AppDefinition
{
    public override void ConfigureApplication(WebApplication app) => app.MapMainEndpointsEndpoints();
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
            .Produces<Ok<StateModel>>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        group.MapGet("add-stream-to-download/{id:guid}/{streamId:int}", AddStreamToDownload)
            .WithName("AddStreamToDownload")
            .WithSummary("Добавить поток к элементу загрузки")
            .WithDescription("Позволяет добавить новый поток к элементу загрузки. Элемент и поток идентифицируются по их уникальным ID.")
            .Produces<Ok<string>>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        group.MapGet("download/{id:guid}/{streamId:int}", Download)
            .WithName("Download")
            .WithSummary("Скачать поток элемента загрузки")
            .WithDescription("Позволяет скачать конкретный поток элемента загрузки. Элемент и поток идентифицируются по их уникальным ID.")
            .Produces<FileStreamHttpResult>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        group.MapGet("state/{id:guid}", GetDownloadItemState)
            .WithName("GetDownloadItemState")
            .WithSummary("Получить состояние элемента загрузки")
            .WithDescription("Возвращает текущее состояние элемента загрузки. Элемент идентифицируется по его уникальному ID.")
            .Produces<Ok<StateModel>>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();
    }

    private static async Task<Results<Ok<StateModel>, BadRequest<string>>> AddToDownload(AddToDownloadRequest request, [FromServices] DownloadService downloadService, ILogger<Endpoint> logger)
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

    private static Results<Ok<string>, BadRequest<string>> AddStreamToDownload(Guid id, int streamId, [FromServices] DownloadService downloadService, ILogger<Endpoint> logger)
    {
        try
        {
            logger.LogInformation("Добавление потока: {StreamId} в очередь загрузки для элемента: {Id}", streamId, id);
            downloadService.SetStreamToDownload(id, streamId);

            logger.LogInformation("Успешно добавлен поток: {StreamId} в очередь загрузки для элемента: {Id}", streamId, id);
            return TypedResults.Ok("Всё оки");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Произошла ошибка при добавлении потока: {StreamId} в очередь загрузки для элемента: {Id}", streamId, id);
            return TypedResults.BadRequest(exception.Message);
        }
    }

    private static Results<FileStreamHttpResult, BadRequest<string>> Download(Guid id, int streamId, [FromServices] DownloadService downloadService, ILogger<Endpoint> logger)
    {
        logger.LogInformation("Скачивание потока: {StreamId} для элемента: {Id}", streamId, id);
        Operation<DownloadItem, string> itemOperation = downloadService.FindItem(id);

        if (itemOperation.Ok == false)
        {
            logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", id, itemOperation.Error);
            return TypedResults.BadRequest(itemOperation.Error);
        }

        DownloadItem item = itemOperation.Result;

        Operation<DownloadItemSteam, string> streamOperation = item.GetStream(streamId);

        if (streamOperation.Ok == false)
        {
            logger.LogError("Не удалось получить поток: {StreamId} для элемента: {Id}, Ошибка: {Error}", streamId, id, streamOperation.Error);
            return TypedResults.BadRequest(streamOperation.Error);
        }

        DownloadItemSteam stream = streamOperation.Result;

        if (stream.State != DownloadItemState.Ready)
        {
            logger.LogError("Поток: {StreamId} для элемента: {Id} не готов, Текущее состояние: {State}", streamId, id, stream.State);
            return TypedResults.BadRequest($"Состояние не готово. Текущее {stream.State}");
        }

        string type = stream.VideoType;
        FileStream fileStream = new(stream.FilePath, FileMode.Open, FileAccess.Read);

        logger.LogInformation("Успешно скачан поток: {StreamId} для элемента: {Id}", streamId, id);
        return TypedResults.Stream(fileStream, $"video/{type}", stream.FileName, enableRangeProcessing: true);
    }

    private static Results<Ok<StateModel>, BadRequest<string>> GetDownloadItemState(Guid id, [FromServices] DownloadService downloadService, ILogger<Endpoint> logger)
    {
        logger.LogInformation("Получение состояния элемента загрузки: {Id}", id);
        Operation<DownloadItem, string> itemOperation = downloadService.FindItem(id);

        if (itemOperation.Ok == false)
        {
            logger.LogError("Не удалось найти элемент загрузки: {Id}, Ошибка: {Error}", id, itemOperation.Error);
            return TypedResults.BadRequest(itemOperation.Error);
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
}