using System.Xml.Linq;

namespace YoutubeDownloader.Logic
{
    public class DownloadManager
    {
        public DownloadManager()
        {
            Items = new List<DownloadItem>();
        }

        public DownloadItem AddToQueue(string url)
        {
            var id = Guid.NewGuid();
            var name = id.ToString() + ".mp4";
            var fullPath = Path.Combine(Globals.Settings.VideoFolderPath, name);
            var item = new DownloadItem
            {
                Id = id,
                State = DownloadItemState.Wait,
                Url = url,
                Name = name,
                FullPath = fullPath,
            };
            Items.Add(item);
            return item;
        }

        internal void DownloadFromQueue()
        {
            var downloadItem = Items.FirstOrDefault(x => x.State == DownloadItemState.Wait);
            if (downloadItem != null)
            {
                downloadItem.State = DownloadItemState.InProcess;
                var info = YoutubeDownloader.Download(downloadItem.Url, downloadItem.FullPath)
                    .GetAwaiter()
                    .GetResult();
                downloadItem.State = DownloadItemState.Ready;
                downloadItem.Info = info;
            }
        }

        public List<DownloadItem> Items { get; set; }

        public class DownloadItem
        {
            public Guid Id { get; set; }
            public DownloadItemState State { get; set; }
            public string Url { get; set; }
            public string Name { get; set; }
            public string FullPath { get; set; }
            public VideoInfo Info { get; set; }
        }
    }
}
