namespace YoutubeDownloader.ApiModels;

public class StateModel
{
    public Guid DownloadId { get; set; }

    public string Title { get; set; }

    public StreamModel[] Streams { get; set; }

    public class StreamModel
    {
        public int Id { get; set; }

        public string State { get; set; }

        public string Title { get; set; }
    }
}