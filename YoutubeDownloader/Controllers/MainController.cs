using Microsoft.AspNetCore.Mvc;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MainController : ControllerBase
    {
        [HttpGet("Download/{url}")]
        public async Task<IActionResult> GetById(string url)
        {
            url = "https://www.youtube.com/watch?v=iVbSzkwx-9M";

            await Downloader.ASD(url);
            return new JsonResult(new { url });
        }
    }

    public class Downloader
    {
        public static async Task ASD(string url)
        {
            var asd = 1;
            var youtube = new YoutubeClient();

            // You can specify either the video URL or its ID
            var videoUrl = "https://youtube.com/watch?v=u_yIGGhubZs";
            var video = await youtube.Videos.GetAsync(videoUrl);

            var title = video.Title; // "Collections - Blender 2.80 Fundamentals"
            var author = video.Author.ChannelTitle; // "Blender"
            var duration = video.Duration; // 00:07:20

            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
            var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            await youtube.Videos.Streams.DownloadAsync(streamInfo, $"video.123");
        }
    }
}