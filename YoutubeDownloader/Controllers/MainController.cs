using Microsoft.AspNetCore.Mvc;
using YoutubeDownloader.ApiModels;
using YoutubeDownloader.Logic;

namespace YoutubeDownloader.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MainController : ControllerBase
    {
        public MainController()
        {
        }

        [HttpPost("AddToDownload")]
        public async Task<IActionResult> AddToDownload(Request model)
        {
            var item = await Globals.DownloadManager.AddToQueueAsync(model.Url);
            StateModel stateModel = GetStateModel(item);
            return new JsonResult(stateModel);
        }

        [HttpGet("state/{id}")]
        public IActionResult State(Guid id)
        {
            var item = Globals.DownloadManager.Items.FirstOrDefault(x => x.Id == id);
            if (item == null)
            {
                return new JsonResult(new { state = "NotFound" });
            }

            StateModel model = GetStateModel(item);
            return new JsonResult(model);
        }

        private static StateModel GetStateModel(DownloadManager.DownloadItem? item)
        {
            var title = item.Video.Title;
            var duration = item.Video.Duration;

            //if (item.State != DownloadItemState.Ready)
            //{
            //    return new JsonResult(new { state = item.State.ToString() });
            //}
            var model = new StateModel
            {
                DownloadId = item.Id,
                Title = title,
                Streams = item.Streams.Select(x => new StateModel.StreamModel
                {
                    Id = x.Id,
                    State = x.State.ToString(),
                    Title = x.Stream.ToString() + " " + Math.Round(x.Stream.Size.MegaBytes, 2) + "МБ",
                }).ToArray(),
            };
            return model;
        }

        [HttpGet("download/{id}/{streamId}")]
        public IActionResult Download(Guid id, int streamId)
        {
            var item = Globals.DownloadManager.Items.FirstOrDefault(x => x.Id == id);
            if (item == null)
            {
                return new JsonResult(new { error = true, message = "Фаил не найден" });
            }
            var stream = item.Streams.FirstOrDefault(x => x.Id == streamId);
            if (stream == null)
            {
                return new JsonResult(new { error = true, message = "Фаил не найден" });
            }

            if (stream.State != DownloadItemState.Ready)
            {
                return new JsonResult(new { error = true, message = "Состояние не готово. Текущие " + stream.State });
            }

            return File(System.IO.File.ReadAllBytes(stream.FullPath), "video/mp4", item.Video.Title + ".mp4");
        }

        [HttpGet("SetToDownloadState/{id}/{streamId}")]
        public IActionResult SetToDownloadState(Guid id, int streamId)
        {
            Globals.DownloadManager.SetStreamToDownload(id, streamId);
            return new JsonResult(new { message = "Всё оки" });
        }

        public class Request
        {
            public string Url { get; set; }
        }
    }
}