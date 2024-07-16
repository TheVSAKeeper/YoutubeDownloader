using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Api.Logic;
using YoutubeDownloader.Api.Models;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace YoutubeDownloader.Api.Endpoints;

public static class MainEndpoints
{
    public static void MapMainEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Main/add-to-download", AddToDownload);
        endpoints.MapGet("/api/Main/state/{id:guid}", GetDownloadItemState);
        endpoints.MapGet("/api/Main/download/{id:guid}/{streamId:int}", Download);
        endpoints.MapGet("/api/Main/add-stream-to-download/{id:guid}/{streamId:int}", AddStreamToDownload);
    }

    private static async Task<StateModel> AddToDownload([FromBody] AddToDownloadRequest request, [FromServices] DownloadManager downloadManager)
    {
        try
        {
            DownloadItem item = await downloadManager.AddToQueueAsync(request.Url);
            Result<StateModel> result = StateModel.Create(item);

            if (result.IsFailure)
            {
                throw new ServiceException(result.Error);
            }

            return result.Value;
        }
        catch (Exception exception)
        {
            throw new ServiceException(exception.Message);
        }
    }

    private static StateModel GetDownloadItemState(Guid id, [FromServices] DownloadManager downloadManager)
    {
        Result<DownloadItem> searchResult = downloadManager.GetItem(id);

        if (searchResult.IsFailure)
        {
            throw new ServiceException(searchResult.Error);
        }

        Result<StateModel> createResult = StateModel.Create(searchResult.Value);

        if (createResult.IsFailure)
        {
            throw new ServiceException(createResult.Error);
        }

        return createResult.Value;
    }

    private static IResult Download(Guid id, int streamId, [FromServices] DownloadManager downloadManager)
    {
        (bool _, bool isFailure, DownloadItem? item, string? error) = downloadManager.GetItem(id);

        if (isFailure)
        {
            throw new ServiceException(error);
        }

        (_, isFailure, DownloadItemSteam? stream, error) = item.GetStream(streamId);

        if (isFailure)
        {
            throw new ServiceException(error);
        }

        if (stream.State != DownloadItemState.Ready)
        {
            throw new ServiceException($"Состояние не готово. Текущие {stream.State}");
        }

        string type = stream.VideoType;
        return Results.File(File.ReadAllBytes(stream.FullPath), $"video/{type}", $"{item.Video.Title}.{type}");
    }

    private static IResult AddStreamToDownload(Guid id, int streamId, [FromServices] DownloadManager downloadManager)
    {
        try
        {
            downloadManager.SetStreamToDownload(id, streamId);

            return Results.Json(new
            {
                message = "Всё оки"
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new
            {
                error = true, message = "Всё упало"
            });
        }
    }
}