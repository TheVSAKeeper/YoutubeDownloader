namespace YoutubeDownloader.Logic
{
    public static class Globals
    {
        public static DownloadManager DownloadManager { get; set; } = new DownloadManager();
        public static Settings Settings { get; set; } 
    }

    public class Settings
    {
        public string VideoFolderPath { get; set; }
    }
}
