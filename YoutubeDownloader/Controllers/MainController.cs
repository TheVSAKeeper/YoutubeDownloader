using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.ApiModels;
using YoutubeDownloader.Logic;

namespace YoutubeDownloader.Controllers;

[ApiController, Route("api/[controller]")]
public class MainController : ControllerBase
{
    private readonly DownloadManager _downloadManager;

    public MainController(DownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
    }

    [HttpPost("AddToDownload")]
    public async Task<IActionResult> AddToDownload(Request model)
    {
        try
        {
            DownloadManager.DownloadItem item = await _downloadManager.AddToQueueAsync(model.Url);
            StateModel stateModel = GetStateModel(item);
            return new JsonResult(stateModel);
        }
        catch (Exception ex)
        {
            // todo сделать мидлварку для ошибок
            return new JsonResult(new { error = true, message = "Всё упало" });
        }
    }

    [HttpGet("state/{id}")]
    public IActionResult State(Guid id)
    {
        DownloadManager.DownloadItem? item = _downloadManager.Items.FirstOrDefault(x => x.Id == id);

        if (item == null)
        {
            return new JsonResult(new { state = "NotFound" });
        }

        StateModel model = GetStateModel(item);
        return new JsonResult(model);
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

    [HttpGet("download/{id}/{streamId}")]
    public IActionResult Download(Guid id, int streamId)
    {
        DownloadManager.DownloadItem? item = _downloadManager.Items.FirstOrDefault(x => x.Id == id);

        if (item == null)
        {
            return new JsonResult(new { error = true, message = "Фаил не найден" });
        }

        DownloadManager.DownloadItemSteam? stream = item.Streams.FirstOrDefault(x => x.Id == streamId);

        if (stream == null)
        {
            return new JsonResult(new { error = true, message = "Фаил не найден" });
        }

        if (stream.State != DownloadItemState.Ready)
        {
            return new JsonResult(new { error = true, message = "Состояние не готово. Текущие " + stream.State });
        }

        string type = stream.VideoType;
        return File(System.IO.File.ReadAllBytes(stream.FullPath), "video/" + type, item.Video.Title + "." + type);
    }

    [HttpGet("SetToDownloadState/{id}/{streamId}")]
    public IActionResult SetToDownloadState(Guid id, int streamId)
    {
        try
        {
            _downloadManager.SetStreamToDownload(id, streamId);
            return new JsonResult(new { message = "Всё оки" });
        }
        catch (Exception ex)
        {
            // todo сделать мидлварку для ошибок
            return new JsonResult(new { error = true, message = "Всё упало" });
        }
    }

    public class Request
    {
        public string Url { get; set; }
    }
}