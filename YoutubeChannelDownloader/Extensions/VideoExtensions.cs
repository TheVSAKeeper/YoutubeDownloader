using System.Text.RegularExpressions;
using YoutubeExplode.Videos;

namespace YoutubeChannelDownloader.Extensions;

public static class VideoExtensions
{
    public static string GetVideoFileName(this IVideo video)
    {
        Regex illegalInFileName = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);
        return illegalInFileName.Replace(video.Title, "_");
    }
}
