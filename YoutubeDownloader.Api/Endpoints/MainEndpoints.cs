using Calabonga.OperationResults;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Api.Logic;
using YoutubeDownloader.Api.Models;
using YoutubeDownloader.Api.Models.Requests;

namespace YoutubeDownloader.Api.Endpoints;

public static class MainEndpoints
{
    public static void MapMainEndpoints(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/api/main/").WithTags("Main");

        group.MapPost("add-to-download", AddToDownload)
            .WithName("AddToDownload")
            .WithSummary("Добавить новый элемент в очередь загрузки")
            .WithDescription("Позволяет добавить новый элемент в очередь загрузки. Элемент идентифицируется по его уникальному ID.")
            .Produces<Ok<StateModel>>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        group.MapGet("state/{id:guid}", GetDownloadItemState)
            .WithName("GetDownloadItemState")
            .WithSummary("Получить состояние элемента загрузки")
            .WithDescription("Возвращает текущее состояние элемента загрузки. Элемент идентифицируется по его уникальному ID.")
            .Produces<Ok<StateModel>>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        group.MapGet("download/{id:guid}/{streamId:int}", Download)
            .WithName("Download")
            .WithSummary("Скачать поток элемента загрузки")
            .WithDescription("Позволяет скачать конкретный поток элемента загрузки. Элемент и поток идентифицируются по их уникальным ID.")
            .Produces<FileStreamHttpResult>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();

        group.MapGet("add-stream-to-download/{id:guid}/{streamId:int}", AddStreamToDownload)
            .WithName("AddStreamToDownload")
            .WithSummary("Добавить поток к элементу загрузки")
            .WithDescription("Позволяет добавить новый поток к элементу загрузки. Элемент и поток идентифицируются по их уникальным ID.")
            .Produces<Ok<string>>()
            .Produces<BadRequest<string>>(StatusCodes.Status400BadRequest)
            .WithOpenApi();
    }

    private static async Task<Results<Ok<StateModel>, BadRequest<string>>> AddToDownload([FromBody] AddToDownloadRequest request, [FromServices] DownloadManager downloadManager)
    {
        try
        {
            DownloadItem item = await downloadManager.AddToQueueAsync(request.Url);
            Operation<StateModel> operation = StateModel.Create(item);

            return operation.Ok ? TypedResults.Ok(operation.Result) : TypedResults.BadRequest("Не удалось получить состояние");
        }
        catch (Exception exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }
    }

    private static Results<Ok<StateModel>, BadRequest<string>> GetDownloadItemState(Guid id, [FromServices] DownloadManager downloadManager)
    {
        Operation<DownloadItem, string> itemOperation = downloadManager.FindItem(id);

        if (itemOperation.Ok == false)
        {
            return TypedResults.BadRequest(itemOperation.Error);
        }

        DownloadItem item = itemOperation.Result;

        Operation<StateModel> stateModelOperation = StateModel.Create(item);

        return stateModelOperation.Ok ? TypedResults.Ok(stateModelOperation.Result) : TypedResults.BadRequest("Не удалось получить состояние");
    }

    private static Results<FileStreamHttpResult, BadRequest<string>> Download(Guid id, int streamId, [FromServices] DownloadManager downloadManager)
    {
        Operation<DownloadItem, string> itemOperation = downloadManager.FindItem(id);

        if (itemOperation.Ok == false)
        {
            return TypedResults.BadRequest(itemOperation.Error);
        }

        DownloadItem item = itemOperation.Result;

        Operation<DownloadItemSteam, string> streamOperation = item.GetStream(streamId);

        if (streamOperation.Ok == false)
        {
            return TypedResults.BadRequest(streamOperation.Error);
        }

        DownloadItemSteam stream = streamOperation.Result;

        if (stream.State != DownloadItemState.Ready)
        {
            return TypedResults.BadRequest($"Состояние не готово. Текущие {stream.State}");
        }

        string type = stream.VideoType;
        FileStream fileStream = new(stream.FullPath, FileMode.Open, FileAccess.Read);

        return TypedResults.Stream(fileStream, $"video/{type}", stream.FileName);
    }

    private static Results<Ok<string>, BadRequest<string>> AddStreamToDownload(Guid id, int streamId, [FromServices] DownloadManager downloadManager)
    {
        try
        {
            downloadManager.SetStreamToDownload(id, streamId);

            return TypedResults.Ok("Всё оки");
        }
        catch (Exception exception)
        {
            return TypedResults.BadRequest(exception.Message);
        }
    }
}