using AngleSharp.Dom;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeChannelDownloader;

public class Helper
{
    public async Task<List<VideoInfo>> Download(string chanelUrl)
    {
        var youtube = new YoutubeClient();
        //var asd = await youtube.Channels.GetAsync(chanelUrl);
        //var asd2 = youtube.Channels.GetUploadsAsync(asd.Id);
        var yVideos = youtube.Channels.GetUploadsAsync(chanelUrl);

        var videos = new List<VideoInfo>();

        await foreach (var item in yVideos)
        {
            var fileName = item.Title
                .Replace("\\", "_")
                .Replace("/", "_")
                .Replace(":", "_")
                .Replace("*", "_")
                .Replace("?", "_")
                .Replace("\"", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("|", "_");
            VideoInfo video = new VideoInfo
            {
                FileName = fileName,
                Status = 0, // не скачано
                Title = item.Title,
                Url = item.Url,
                ThumbnailUrl = item.Thumbnails.OrderByDescending(x => x.Resolution.Width).FirstOrDefault()?.Url,
                PlaylistId = item.PlaylistId.Value,
            };
            videos.Add(video);
            Console.WriteLine("add video: " + item.Title);
        }

        return videos;
    }

    public async Task GetItem(VideoInfo videoInfo, string path)
    {
        var url = videoInfo.Url;
        var youtube = new YoutubeClient();
        var video = await youtube.Videos.GetAsync(url);

        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

        var id = Guid.NewGuid();

        var streams = streamManifest.Streams.Select((x, i) =>
                new DownloadItemSteam
                {
                    Id = i,
                    Name = id.ToString() + "_" + i + ".mp4",
                    Stream = streamManifest.Streams[i],
                }).ToList();
        var streamId = streams.Count();

        var types = new List<string> { "mp4" };
        foreach (var type in types)
        {
            var bestMuxedStream = streams.Where(x => x.Stream != null && x.Stream.Container.Name == type && x.Stream is MuxedStreamInfo)
                .OrderByDescending(x => ((MuxedStreamInfo)x.Stream).VideoQuality.MaxHeight).FirstOrDefault();
            var bestVideoStream = streams.Where(x => x.Stream != null && x.Stream.Container.Name == type && x.Stream is VideoOnlyStreamInfo)
                .OrderByDescending(x => ((VideoOnlyStreamInfo)x.Stream).VideoQuality.MaxHeight).FirstOrDefault();
            var bestAudioStream = streams.Where(x => x.Stream != null && x.Stream.Container.Name == type && x.Stream is AudioOnlyStreamInfo)
                .OrderByDescending(x => ((AudioOnlyStreamInfo)x.Stream).Size).FirstOrDefault();
            int muxedMaxHeight = 0;
            if (bestMuxedStream != null)
            {
                muxedMaxHeight = ((MuxedStreamInfo)bestMuxedStream.Stream).VideoQuality.MaxHeight;
            }

            if (bestVideoStream != null && bestAudioStream != null)
            {
                if (muxedMaxHeight < ((VideoOnlyStreamInfo)bestVideoStream.Stream).VideoQuality.MaxHeight)
                {
                    streams.Insert(0, new DownloadItemSteam
                    {
                        Id = streamId,
                        Name = videoInfo.FileName,
                        IsCombineAfterDownload = true,
                        CombineAfterDownloadStreamAudio = bestAudioStream.Stream,
                        CombineAfterDownloadStreamVideo = bestVideoStream.Stream,
                    });
                    streamId++;
                }
            }
        }

        streams[0].FullPath = Path.Combine(path, streams[0].Name);
        await DownloadFromQueue(streams[0]);
        File.WriteAllText(Path.Combine(path, videoInfo.FileName + "_title.txt"), videoInfo.Title);
        File.WriteAllText(Path.Combine(path, videoInfo.FileName + "_description.txt"), video.Description);
    }

    public async Task DownloadFromQueue(DownloadItemSteam downloadStream)
    {
        if (downloadStream.IsCombineAfterDownload)
        {
            var audioPath = downloadStream.FullPath + "_audio." + downloadStream.CombineAfterDownloadStreamVideo.Container.Name;
            var videoPath = downloadStream.FullPath + "_video." + downloadStream.CombineAfterDownloadStreamVideo.Container.Name;


            var task = YoutubeDownloader.Download(downloadStream.CombineAfterDownloadStreamAudio, audioPath);
            var task2 = YoutubeDownloader.Download(downloadStream.CombineAfterDownloadStreamVideo, videoPath);
            Task.WaitAll(task, task2);

            var args = "-i \"" + videoPath + "\" -i \"" + audioPath + "\" -c copy \"" + downloadStream.FullPath + "\"";
            await RunFFMPEG(args);
        }
        else
        {
            await YoutubeDownloader.Download(downloadStream.Stream, downloadStream.FullPath);
        }
    }


    public async Task RunFFMPEG(string ffmpegCommand)
    {
        using Process process = new Process();
        ProcessStartInfo processStartInfo2 = (process.StartInfo = new ProcessStartInfo
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = "C:\\Services\\utils\\ffmpeg\\ffmpeg.exe",
            RedirectStandardError = true,
            Arguments = ffmpegCommand
        });
        process.Start();


        string lastLine = null;
        StringBuilder runMessage = new StringBuilder();
        while (!process.StandardError.EndOfStream)
        {
            string text = await process.StandardError.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
            runMessage.AppendLine(text);
            lastLine = text;
        }

        await process.WaitForExitAsync().ConfigureAwait(continueOnCapturedContext: false);
        if (process.ExitCode != 0)
        {
        }
    }

    public class YoutubeDownloader
    {
        public static async Task Download(IStreamInfo stream, string path)
        {
            var youtube = new YoutubeClient();
            await youtube.Videos.Streams.DownloadAsync(stream, path);
        }
    }

    public class DownloadItem
    {
        public Guid Id { get; set; }
        public string Url { get; set; }
        public List<DownloadItemSteam> Streams { get; set; }
        public Video Video { get; internal set; }
    }

    public class DownloadItemSteam
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public IStreamInfo Stream { get; set; }

        public bool IsCombineAfterDownload { get; set; }
        public IStreamInfo CombineAfterDownloadStreamAudio { get; set; }
        public IStreamInfo CombineAfterDownloadStreamVideo { get; set; }


        public string Title
        {
            get
            {
                if (IsCombineAfterDownload)
                {
                    var video = (VideoOnlyStreamInfo)CombineAfterDownloadStreamVideo;
                    return "Muxed (" + video.VideoQuality.MaxHeight + " | " + video.Container.Name + ") ~" + SizeMB + "МБ";
                }
                else
                {
                    return Stream.ToString() + " " + SizeMB + "МБ";
                }
            }
        }

        public double SizeMB
        {
            get
            {
                if (IsCombineAfterDownload)
                {
                    var size = CombineAfterDownloadStreamAudio.Size.MegaBytes + CombineAfterDownloadStreamVideo.Size.MegaBytes;
                    return Math.Round(size, 2);
                }
                else
                {
                    return Math.Round(Stream.Size.MegaBytes, 2);
                }
            }
        }

        public string VideoType
        {
            get
            {
                if (IsCombineAfterDownload)
                {
                    var video = (VideoOnlyStreamInfo)CombineAfterDownloadStreamVideo;
                    return video.Container.Name;
                }
                else
                {
                    return Stream.Container.Name;
                }
            }
        }

        public Action AfterDownloadAction { get; set; }
    }
}
