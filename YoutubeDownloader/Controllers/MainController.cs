using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
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
            var item = Globals.DownloadManager.AddToQueue(model.Url);
            return new JsonResult(new { downloadId = item.Id });
        }

        [HttpGet("state/{id}")]
        public IActionResult State(Guid id)
        {
            var item = Globals.DownloadManager.Items.FirstOrDefault(x => x.Id == id);
            if (item == null)
            {
                return new JsonResult(new { state = "NotFound" });
            }

            if (item.State != DownloadItemState.Ready)
            {
                return new JsonResult(new { state = item.State.ToString() });
            }

            return new JsonResult(new { state = item.State.ToString(), title = item.Info.Title, size = item.Info.BiteSize });
        }

        [HttpGet("download/{id}")]
        public IActionResult Download(Guid id)
        {
            var item = Globals.DownloadManager.Items.FirstOrDefault(x => x.Id == id);
            if (item == null)
            {
                return new JsonResult(new { error = true, message = "Фаил не найден" });
            }

            if (item.State != DownloadItemState.Ready)
            {
                return new JsonResult(new { error = true, message = "Состояние не готово. Текущие " + item.State });
            }

            return File(System.IO.File.ReadAllBytes(item.FullPath), "video/mp4", item.Info.Title + ".mp4");
        }

        public class Request
        {
            public string Url { get; set; }
        }
    }
}