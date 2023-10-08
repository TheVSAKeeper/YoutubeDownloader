using System.Xml.Linq;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using static YoutubeDownloader.Logic.DownloadManager;

namespace YoutubeDownloader.Logic
{
    public class DownloadManager
    {
        public DownloadManager()
        {
            Items = new List<DownloadItem>();
        }

        public async Task<DownloadItem> AddToQueueAsync(string url)
        {
            var youtube = new YoutubeClient();

            var video = await youtube.Videos.GetAsync(url);

            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();

            var id = Guid.NewGuid();

            var item = new DownloadItem
            {
                Id = id,
                Url = url,
                Video = video,
                Streams = streamManifest.Streams.Select((x, i) =>
                    new DownloadItemSteam
                    {
                        Id = i,
                        Name = id.ToString() + "_" + i + ".mp4",
                        FullPath = Path.Combine(Globals.Settings.VideoFolderPath, id.ToString() + "_" + i + ".mp4"),
                        Stream = streamManifest.Streams[i],
                        State = DownloadItemState.Base
                    }).ToArray()
            };
            Items.Add(item);
            return item;
        }

        public void SetStreamToDownload(Guid downloadId, int streamId)
        {
            var downloadItem = Items.First(x => x.Id == downloadId);
            var stream = downloadItem.Streams.First(x => x.Id == streamId);
            stream.State = DownloadItemState.Wait;
        }

        public void DownloadFromQueue()
        {
            var downloadItem = Items.FirstOrDefault(x => x.Streams.Any(x => x.State == DownloadItemState.Wait));
            if (downloadItem != null)
            {
                var downloadStream = downloadItem.Streams.First(x => x.State == DownloadItemState.Wait);
                downloadStream.State = DownloadItemState.InProcess;
                var info = YoutubeDownloader.Download(downloadStream.Stream, downloadStream.FullPath)
                    .GetAwaiter()
                    .GetResult();
                downloadStream.State = DownloadItemState.Ready;
                downloadItem.Info = info;
            }
        }

        public List<DownloadItem> Items { get; set; }

        public class DownloadItem
        {
            public Guid Id { get; set; }
            public string Url { get; set; }
            public VideoInfo Info { get; set; }
            public DownloadItemSteam[] Streams { get; set; }
            public Video Video { get; internal set; }
        }

        public class DownloadItemSteam
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string FullPath { get; set; }
            public DownloadItemState State { get; set; }
            public IStreamInfo Stream { get; set; }
        }
    }
}
