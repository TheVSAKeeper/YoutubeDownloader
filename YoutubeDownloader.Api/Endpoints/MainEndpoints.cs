using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.Api.Logic;
using YoutubeDownloader.Api.Models;

namespace YoutubeDownloader.Api.Endpoints;

public static class MainEndpoints
{
    public static void MapMainEndpoints(this IEndpointRouteBuilder endpoints, IServiceProvider serviceProvider)
    {
        endpoints.MapPost("/api/Main/AddToDownload", async ([FromBody] Request model) =>
        {
            DownloadManager downloadManager = serviceProvider.GetRequiredService<DownloadManager>();

            try
            {
                DownloadManager.DownloadItem item = await downloadManager.AddToQueueAsync(model.Url);
                StateModel stateModel = GetStateModel(item);
                return Results.Json(stateModel);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = true, message = "Всё упало" });
            }
        });

        endpoints.MapGet("/api/Main/state/{id}", (Guid id) =>
        {
            DownloadManager downloadManager = serviceProvider.GetRequiredService<DownloadManager>();
            DownloadManager.DownloadItem? item = downloadManager.Items.FirstOrDefault(x => x.Id == id);

            if (item == null)
            {
                return Results.Json(new { state = "NotFound" });
            }

            StateModel model = GetStateModel(item);
            return Results.Json(model);
        });

        endpoints.MapGet("/api/Main/download/{id}/{streamId}", (Guid id, int streamId) =>
        {
            DownloadManager downloadManager = serviceProvider.GetRequiredService<DownloadManager>();
            DownloadManager.DownloadItem? item = downloadManager.Items.FirstOrDefault(x => x.Id == id);

            if (item == null)
            {
                return Results.Json(new { error = true, message = "Фаил не найден" });
            }

            DownloadManager.DownloadItemSteam? stream = item.Streams.FirstOrDefault(x => x.Id == streamId);

            if (stream == null)
            {
                return Results.Json(new { error = true, message = "Фаил не найден" });
            }

            if (stream.State != DownloadItemState.Ready)
            {
                return Results.Json(new { error = true, message = $"Состояние не готово. Текущие {stream.State}" });
            }

            string type = stream.VideoType;
            return Results.File(File.ReadAllBytes(stream.FullPath), $"video/{type}", $"{item.Video.Title}.{type}");
        });

        endpoints.MapGet("/api/Main/SetToDownloadState/{id:guid}/{streamId:int}", (Guid id, int streamId) =>
        {
            DownloadManager downloadManager = serviceProvider.GetRequiredService<DownloadManager>();

            try
            {
                downloadManager.SetStreamToDownload(id, streamId);
                return Results.Json(new { message = "Всё оки" });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = true, message = "Всё упало" });
            }
        });
    }

    private static StateModel GetStateModel(DownloadManager.DownloadItem? item)
    {
        string title = item.Video.Title;
        TimeSpan? duration = item.Video.Duration;

        StateModel model = new()
        {
            DownloadId = item.Id,
            Title = title,
            Streams = item.Streams.Select(x => new StateModel.StreamModel
                {
                    Id = x.Id,
                    State = x.State.ToString(),
                    Title = x.Title
                })
                .ToArray()
        };

        return model;
    }
}

public class Request
{
    public string Url { get; set; }
}