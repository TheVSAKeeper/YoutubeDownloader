using System.Text.RegularExpressions;

namespace YoutubeDownloader.Api.Application.Extensions;

public static class VideoExtensions
{
    public static string GetVideoFileName(this Video video)
    {
        Regex illegalInFileName = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);
        return illegalInFileName.Replace(video.Title, "_");
    }
}