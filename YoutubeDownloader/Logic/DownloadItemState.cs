namespace YoutubeDownloader.Logic
{
    public enum DownloadItemState
    {
        /// <summary>
        /// Ожидает дальнейших распоряжений.
        /// </summary>
        Base,

        /// <summary>
        /// Ожидает загрузку.
        /// </summary>
        Wait,

        /// <summary>
        /// В процессе загрузки.
        /// </summary>
        InProcess,

        /// <summary>
        /// Загружен.
        /// </summary>
        Ready,

        Error,
    }
}
