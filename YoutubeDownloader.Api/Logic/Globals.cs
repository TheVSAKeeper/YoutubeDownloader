namespace YoutubeDownloader.Api.Logic;

public static class Globals
{
    public static Settings Settings { get; set; }
}

public class Settings
{
    public string VideoFolderPath { get; set; }
    public string TelegramBotTokenPath { get; set; }
}